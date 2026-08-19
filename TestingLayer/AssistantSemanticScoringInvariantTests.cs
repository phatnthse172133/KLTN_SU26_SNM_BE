using System.Text.Json;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.Assistant;
using ApplicationLayer.Services.Carts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Options;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

/// <summary>
/// Generic Stage B scoring invariants via mocked positional scores and the 0.55 relevance gate.
/// No runtime food-specific branches — only rubric-aligned score distributions.
/// </summary>
public sealed class AssistantSemanticScoringInvariantTests
{
    private const double RelevanceFloor = 0.55;

    [Fact]
    public void A_SingleIngredientOverlap_NotDirectMatchLevel()
    {
        var incidental = Eligible("Món A", ingredient: "CHEESE");
        var semantic = SemanticScores((incidental.FoodItem.Id, 0.35));
        var ranked = CreateScorer().Score(
            [incidental],
            SpecificDishIntent("layered baked pasta with cheese"),
            semantic,
            DateTime.UtcNow);

        Assert.Empty(ranked);
        Assert.False(AssistantSemanticRelevanceGate.IsRelevant(0.35, OptionsWithFloor()));
    }

    [Fact]
    public void B_SameCuisineDifferentDish_BelowCloseMatch()
    {
        var sameRegion = Eligible("Regional noodle soup", prep: "SOUP_COOKED");
        var semantic = SemanticScores((sameRegion.FoodItem.Id, 0.42));
        var ranked = CreateScorer().Score(
            [sameRegion],
            SpecificDishIntent("specific grilled skewer dish"),
            semantic,
            DateTime.UtcNow);

        Assert.Empty(ranked);
    }

    [Fact]
    public void C_StrongSurvivesGate_WeakRejected()
    {
        var strong = Eligible("Direct match", distanceMeters: 900);
        var weak = Eligible("Incidental overlap", distanceMeters: 50);
        var semantic = SemanticScores(
            (strong.FoodItem.Id, 0.82),
            (weak.FoodItem.Id, 0.38));
        var ranked = CreateScorer().Score(
            [weak, strong],
            new ParsedAssistantIntent
            {
                Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
                SemanticPreferences = ["requested specialty"]
            },
            semantic,
            DateTime.UtcNow);

        var item = Assert.Single(ranked);
        Assert.Equal(strong.FoodItem.Id, item.Eligible.FoodItem.Id);
        Assert.True(item.SemanticScore >= RelevanceFloor);
    }

    [Fact]
    public void D_NoGenuineMatch_ReturnsEmptyList()
    {
        var a = Eligible("Candidate A");
        var b = Eligible("Candidate B");
        var semantic = SemanticScores(
            (a.FoodItem.Id, 0.28),
            (b.FoodItem.Id, 0.31));
        var ranked = CreateScorer().Score(
            [a, b],
            SpecificDishIntent("out-of-catalog specialty"),
            semantic,
            DateTime.UtcNow);

        Assert.Empty(ranked);
    }

    [Fact]
    public void E_BroadRequest_ReasonableCandidatesPass()
    {
        var featured = Eligible("Popular snack", featured: true, rating: 4.5m, reviews: 12);
        var semantic = SemanticScores((featured.FoodItem.Id, 0.68));
        var ranked = CreateScorer().Score(
            [featured],
            new ParsedAssistantIntent
            {
                Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
                SemanticPreferences = ["something tasty tonight"]
            },
            semantic,
            DateTime.UtcNow);

        Assert.Single(ranked);
    }

    [Fact]
    public void F_TastePreference_RelatedFoodsPass()
    {
        var spicy = Eligible("Spicy grill");
        spicy.FoodItem.TasteProfiles =
        [
            new FoodItemTasteProfile
            {
                FoodItemId = spicy.FoodItem.Id,
                TasteProfile = new TasteProfile { Id = Guid.NewGuid(), Code = "SPICY", Name = "Spicy", IsActive = true },
                TasteProfileId = Guid.NewGuid()
            }
        ];
        var semantic = SemanticScores((spicy.FoodItem.Id, 0.77));
        var ranked = CreateScorer().Score(
            [spicy],
            new ParsedAssistantIntent
            {
                Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
                StructuredPreferences = new AssistantStructuredPreferences { PreferredTasteCodes = ["SPICY"] },
                SemanticPreferences = ["extra spicy"]
            },
            semantic,
            DateTime.UtcNow);

        Assert.Single(ranked);
    }

    [Fact]
    public async Task G_UnseenPrompt_PipelineHonorsPositionalScoresAndGate()
    {
        var llm = new Mock<ILanguageModelClient>();
        var food = Eligible("Warm comfort bowl");
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()))
            .Returns((string system, string user, int _, CancellationToken _, LanguageModelJsonSchemaOptions? _) =>
            {
                if (system.Contains("STAGE A", StringComparison.Ordinal))
                {
                    return Task.FromResult<LanguageModelJsonCompletion>(
                        """{"intent":"FOOD_RECOMMENDATION","needsLocation":false,"hardConstraints":{},"structuredPreferences":{},"semanticPreferences":["warm gentle comfort"],"semanticAvoidances":[]}""");
                }

                if (system.Contains("STAGE B", StringComparison.Ordinal))
                {
                    Assert.Contains("TRỰC TIẾP", system, StringComparison.Ordinal);
                    Assert.Contains("KHÁI NIỆM TRUNG TÂM", system, StringComparison.Ordinal);
                    var ids = CandidateIds(user);
                    return Task.FromResult<LanguageModelJsonCompletion>(ScoresJson(ids, 0.72));
                }

                throw new InvalidOperationException(system);
            });

        var customerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var service = CreatePipelineService(llm.Object, customerId, conversationId, food);
        var result = await service.SendMessageAsync(
            customerId,
            conversationId,
            new SendAssistantMessageRequest { Message = "Trời lạnh muốn cái gì ấm bụng dịu nhẹ." },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(food.FoodItem.Id, Assert.Single(result.Data!.Recommendations).FoodItemId);
    }

    [Fact]
    public void SemanticSystemPrompt_DefinesDirectSuitabilityRubric()
    {
        var prompt = AssistantPromptCatalog.SemanticSystem;
        Assert.Contains("STAGE B", prompt, StringComparison.Ordinal);
        Assert.Contains("TRỰC TIẾP", prompt, StringComparison.Ordinal);
        Assert.Contains("KHÁI NIỆM TRUNG TÂM", prompt, StringComparison.Ordinal);
        Assert.Contains("NEAR ZERO", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("lasagna", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplyComposer_EmptyRecommendations_ReturnsNoMatchMessage()
    {
        var reply = new AssistantReplyComposer().Compose(
            new ParsedAssistantIntent { Intent = AssistantIntentKind.FOOD_RECOMMENDATION },
            [],
            []);

        Assert.Contains("chưa có món", reply, StringComparison.OrdinalIgnoreCase);
    }

    private static AssistantCompatibilityScorer CreateScorer()
        => new(Options.Create(new AssistantOptions
        {
            MinimumSemanticRelevanceScore = RelevanceFloor,
            MinimumCompatibilityScore = 0,
            SemanticWeight = 0.40,
            StructuredPreferenceWeight = 0.20,
            PriceWeight = 0.15,
            RatingWeight = 0.10,
            FeaturedWeight = 0.03,
            PromoWeight = 0.02
        }));

    private static AssistantOptions OptionsWithFloor()
        => new() { MinimumSemanticRelevanceScore = RelevanceFloor };

    private static ParsedAssistantIntent SpecificDishIntent(string semanticPreference)
        => new()
        {
            Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
            SemanticPreferences = [semanticPreference]
        };

    private static AssistantSemanticMatchResult SemanticScores(params (Guid Id, double Score)[] scores)
        => new()
        {
            Scores = scores.ToDictionary(
                item => item.Id,
                item => new AssistantSemanticScore
                {
                    FoodItemId = item.Id,
                    SemanticCompatibility = item.Score
                })
        };

    private static AssistantService CreatePipelineService(
        ILanguageModelClient llm,
        Guid customerId,
        Guid conversationId,
        AssistantEligibleFood food)
    {
        var conversations = new Mock<IAssistantConversationRepository>();
        var foods = new Mock<IAssistantFoodQueryRepository>();
        var metadata = new Mock<IFoodSemanticMetadataRepository>();
        var conversation = new AssistantConversation
        {
            Id = conversationId,
            CustomerId = customerId,
            Status = AssistantConversationStatus.Active,
            Messages = [],
            MealPlans = []
        };
        conversations.Setup(r => r.GetOwnedAsync(conversationId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        conversations.Setup(r => r.GetRecentMessagesAsync(conversationId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        conversations.Setup(r => r.SaveTurnAsync(It.IsAny<AssistantConversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        conversations.Setup(r => r.AddMessage(It.IsAny<AssistantMessage>()))
            .Callback<AssistantMessage>(message => conversation.Messages.Add(message));
        metadata.Setup(r => r.GetActiveCatalogsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoodSemanticCatalogSet([], [], [], [], []));
        foods.Setup(r => r.CountNotDeletedFoodItemsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        foods.Setup(r => r.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantFoodQueryResult
            {
                Foods = [food],
                Pipeline = new AssistantFoodQueryPipelineDiagnostics { AfterHardConstraints = 1 }
            });

        var assistantOptions = Options.Create(new AssistantOptions
        {
            MinimumSemanticRelevanceScore = RelevanceFloor,
            MinimumCompatibilityScore = 0,
            CandidateBatchSize = 8
        });
        var openAi = Options.Create(new OpenAiOptions { Enabled = true, ApiKey = "test-key" });
        return new AssistantService(
            conversations.Object,
            foods.Object,
            metadata.Object,
            new Mock<ICartService>().Object,
            new AssistantIntentInterpreter(llm, openAi),
            new AssistantSemanticMatcher(llm, assistantOptions, openAi),
            new AssistantCompatibilityScorer(assistantOptions),
            new AssistantMealPlanComposer(llm, new AssistantMealPlanValidator(assistantOptions), assistantOptions, openAi),
            new AssistantReplyComposer(),
            assistantOptions,
            openAi,
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AssistantService>.Instance);
    }

    private static IReadOnlyList<Guid> CandidateIds(string user)
    {
        using var document = JsonDocument.Parse(user);
        return document.RootElement.GetProperty("candidates")
            .EnumerateArray()
            .Select(item => item.GetProperty("foodItemId").GetGuid())
            .ToArray();
    }

    private static LanguageModelJsonCompletion ScoresJson(IReadOnlyList<Guid> ids, double score)
        => LanguageModelJsonCompletion.FromContent(JsonSerializer.Serialize(new
        {
            scores = ids.Select(_ => score).ToArray()
        }));

    private static AssistantEligibleFood Eligible(
        string name,
        string? ingredient = null,
        string? prep = null,
        bool featured = false,
        decimal rating = 0,
        int reviews = 0,
        double? distanceMeters = null)
    {
        var market = new NightMarket
        {
            Id = Guid.NewGuid(),
            Name = "Chợ",
            Status = NightMarketStatus.Active,
            OpeningHours = new TimeOnly(0, 0),
            ClosingHours = new TimeOnly(0, 0)
        };
        var booth = new Booth { Id = Guid.NewGuid(), BoothName = "Quầy", NightMarket = market, NightMarketId = market.Id, Status = BoothStatus.Active };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            Booth = booth,
            BoothId = booth.Id,
            Category = new FoodCategory { Id = Guid.NewGuid(), Name = "Món", Code = "MAIN" },
            IsFeatured = featured,
            AverageRating = rating,
            ReviewCount = reviews,
            Ingredients = [],
            TasteProfiles = [],
            PreparationMethods = [],
            Courses = []
        };
        if (ingredient is not null)
        {
            var ing = new Ingredient { Id = Guid.NewGuid(), Code = ingredient, Name = ingredient, IsActive = true };
            food.Ingredients.Add(new FoodItemIngredient { FoodItemId = food.Id, IngredientId = ing.Id, Ingredient = ing, FoodItem = food });
        }
        if (prep is not null)
        {
            var method = new PreparationMethod { Id = Guid.NewGuid(), Code = prep, Name = prep, IsActive = true };
            food.PreparationMethods.Add(new FoodItemPreparationMethod { FoodItemId = food.Id, PreparationMethodId = method.Id, PreparationMethod = method });
        }

        return new AssistantEligibleFood { FoodItem = food, EffectivePrice = 40_000m, DistanceMeters = distanceMeters };
    }
}
