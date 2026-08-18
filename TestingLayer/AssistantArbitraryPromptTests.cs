using System.Text.Json;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Assistant;
using ApplicationLayer.Services.Carts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class AssistantArbitraryPromptTests
{
    private readonly Mock<ILanguageModelClient> _llm = new();
    private readonly Mock<IAssistantConversationRepository> _conversations = new();
    private readonly Mock<IAssistantFoodQueryRepository> _foods = new();
    private readonly Mock<IFoodSemanticMetadataRepository> _metadata = new();
    private readonly Mock<INightMarketRepository> _markets = new();
    private readonly Mock<ICartService> _carts = new();
    private readonly AssistantConversation _conversation;
    private readonly AssistantOptions _options;
    private readonly AssistantService _service;

    public AssistantArbitraryPromptTests()
    {
        _conversation = new AssistantConversation
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Status = AssistantConversationStatus.Active,
            Messages = new List<AssistantMessage>(),
            MealPlans = new List<AssistantMealPlan>()
        };
        _conversations.Setup(repository => repository.GetOwnedAsync(_conversation.Id, _conversation.CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_conversation);
        _conversations.Setup(repository => repository.GetRecentMessagesAsync(_conversation.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _conversations.Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _conversations.Setup(repository => repository.SaveTurnAsync(It.IsAny<AssistantConversation>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _conversations.Setup(repository => repository.AddMessage(It.IsAny<AssistantMessage>()))
            .Callback<AssistantMessage>(message => _conversation.Messages.Add(message));
        _conversations.Setup(repository => repository.AddMealPlan(It.IsAny<AssistantMealPlan>()))
            .Callback<AssistantMealPlan>(plan => _conversation.MealPlans.Add(plan));
        _metadata.Setup(repository => repository.GetActiveCatalogsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalog());
        _foods.Setup(repository => repository.CountNotDeletedFoodItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(14);

        _options = new AssistantOptions
        {
            CandidateBatchSize = 30,
            MaxRecommendations = 8,
            MinimumCompatibilityScore = 0.4,
            MaxMealPlanOptions = 3,
            MaxMealPlanCandidates = 40,
            MaxMealPlanItemsPerPlan = 8
        };
        var assistantOptions = Options.Create(_options);
        var openAi = Options.Create(new OpenAiOptions { Enabled = true, ApiKey = "test-key" });
        _service = new AssistantService(
            _conversations.Object,
            _foods.Object,
            _metadata.Object,
            _markets.Object,
            _carts.Object,
            new AssistantIntentInterpreter(_llm.Object, openAi),
            new AssistantSemanticMatcher(_llm.Object, assistantOptions, openAi),
            new AssistantCompatibilityScorer(assistantOptions),
            new AssistantMealPlanComposer(_llm.Object, new AssistantMealPlanValidator(assistantOptions), assistantOptions, openAi),
            new AssistantReplyComposer(),
            assistantOptions,
            openAi,
            TimeProvider.System,
            NullLogger<AssistantService>.Instance);
    }

    [Theory]
    [InlineData("Tìm 1 món ngon ngon cho hôm nay.", "ngon ngon")]
    [InlineData("Tôi muốn ăn gì đó hấp dẫn.", "hấp dẫn")]
    [InlineData("Hôm nay tôi không biết ăn gì.", "không biết ăn gì")]
    [InlineData("Tôi muốn một món đáng thử.", "đáng thử")]
    public async Task Recommend_ArbitraryPrompt_UsesStageASemanticThenStageB(string prompt, string leftover)
    {
        var food = Eligible("Bún bò Huế", 45_000m, rating: 4.6m, reviews: 12);
        string? stageBPayload = null;
        SetupPipeline(
            RecommendIntent(leftover),
            [food],
            stageB: (ids, user) =>
            {
                stageBPayload = user;
                return ScoresJson(ids, 0.82, reasons: ["khớp mô tả"], unknown: []);
            });

        var result = await Send(prompt);

        Assert.True(result.Success);
        var rec = Assert.Single(result.Data!.Recommendations);
        Assert.Equal(food.FoodItem.Id, rec.FoodItemId);
        Assert.Equal("Bún bò Huế", rec.Name);
        Assert.Equal(45_000m, rec.EffectivePrice);
        Assert.Contains(leftover, result.Data.PreferenceSummary.SemanticPreferences);
        Assert.Contains("soldToday", stageBPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chất lượng cao", result.Data.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recommend_Bestseller_WithZeroSales_IsUnknown_NotFeaturedRemap()
    {
        var food = Eligible("Cơm tấm", 40_000m, rating: 4.9m, reviews: 1, featured: true, soldToday: 0, orderCount: 0);
        string? stageBPayload = null;
        SetupPipeline(
            RecommendIntent("bestseller hôm nay"),
            [food],
            stageB: (ids, user) =>
            {
                stageBPayload = user;
                Assert.Contains("\"soldToday\":0", user.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
                return ScoresJson(ids, 0.7, reasons: ["MISSING_DB_FIELD"], unknown: ["sales"]);
            });

        var result = await Send("Tôi muốn kiếm món bestseller cho hôm nay.");

        Assert.True(result.Success);
        var rec = Assert.Single(result.Data!.Recommendations);
        Assert.Contains("sales", rec.UnknownDataFacets);
        Assert.DoesNotContain(rec.Reasons, reason => reason.Contains("featured", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(rec.Reasons, reason => reason.Contains("bán chạy", StringComparison.OrdinalIgnoreCase));
        Assert.True(rec.IsFeatured);
        Assert.Contains("soldToday", stageBPayload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lượng bán", result.Data.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MealPlan_FamilySharingVariety_ComposesFromStageC()
    {
        var marketId = Guid.NewGuid();
        var main = Eligible("Lẩu thái", 120_000m, course: FoodCourse.SHARED_DISH, shareable: true, serving: 4, rating: 4.4m, reviews: 8, marketId: marketId);
        var side = Eligible("Gỏi", 45_000m, course: FoodCourse.SIDE_DISH, marketId: marketId);
        SetupPipeline(
            MealIntent(partySize: 4, budget: 400_000m, context: "gia đình dùng chung", goal: "đa dạng", prefs: ["variety", "shareable"]),
            [main, side],
            stageC: PlansJson(
                title: null,
                items: [(main.FoodItem.Id, 1, "SHARED_DISH"), (side.FoodItem.Id, 1, "SIDE_DISH")]));

        var result = await Send("Thực đơn gia đình 4 người, thích chia sẻ và đa dạng.");

        Assert.Equal(AssistantIntentKind.MEAL_PLAN, result.Data!.Intent);
        var plan = Assert.Single(result.Data.MealPlans);
        Assert.Equal(4, plan.PartySize);
        Assert.Equal(165_000m, plan.TotalPrice);
        Assert.Equal(165_000m, plan.EstimatedTotal);
        Assert.Equal(235_000m, plan.RemainingBudget);
        Assert.Null(plan.Title);
        Assert.Contains(plan.Sections, section => section.Course == "SHARED_DISH" && section.Items.Count == 1);
        Assert.DoesNotContain(plan.Sections, section => section.Course == "DESSERT");
        Assert.DoesNotContain("Ưu tiên đa dạng", result.Data.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MealPlan_TastyDinner_DoesNotClaimHighQualityWithoutEvidence()
    {
        var main = Eligible("Cá nướng", 70_000m, course: FoodCourse.MAIN_COURSE, rating: 4.7m, reviews: 9);
        SetupPipeline(
            MealIntent(partySize: 2, budget: 200_000m, prefs: ["bữa ăn ngon"]),
            [main],
            stageC: PlansJson(overall: "rating 4.7 với 9 review", items: [(main.FoodItem.Id, 2, "MAIN_COURSE")]));

        var result = await Send("Soạn bữa ăn ngon cho 2 người.");

        Assert.DoesNotContain("chất lượng cao", result.Data!.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chất lượng cao", result.Data.MealPlans[0].OverallPlanReason ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(140_000m, result.Data.MealPlans[0].TotalPrice);
    }

    [Fact]
    public async Task MealPlan_GrilledFriends_UsesLlmMappedPrepAndContext()
    {
        var grill = Eligible("Ba chỉ nướng", 90_000m, course: FoodCourse.MAIN_COURSE, prep: "GRILLED");
        SetupPipeline(
            """
            {
              "intent": "MEAL_PLAN", "needsLocation": false, "partySize": 3, "budgetMax": 300000,
              "hardConstraints": {},
              "structuredPreferences": { "preferredPreparationCodes": ["GRILLED"] },
              "semanticPreferences": ["đi với bạn bè"],
              "semanticAvoidances": [],
              "diningContext": "nhóm bạn",
              "userGoal": "ăn cùng nhau"
            }
            """,
            [grill],
            stageC: PlansJson(items: [(grill.FoodItem.Id, 2, "MAIN_COURSE")]));

        var result = await Send("Muốn đồ nướng đi với bạn bè, 3 người.");

        Assert.Contains("GRILLED", result.Data!.PreferenceSummary.StructuredPreferences);
        Assert.Equal("nhóm bạn", result.Data.Diagnostics!.ParsedIntent!.DiningContext);
        Assert.Contains("đi với bạn bè", result.Data.Diagnostics.ParsedIntent.SemanticPreferences);
        Assert.Equal("ăn cùng nhau", result.Data.Diagnostics.ParsedIntent.UserGoal);
        Assert.Equal(180_000m, result.Data.MealPlans[0].TotalPrice);
    }

    [Fact]
    public async Task MealPlan_ManyDishesAndValueDinner_ReturnGroundedPlans()
    {
        var marketId = Guid.NewGuid();
        var a = Eligible("Bánh tráng", 25_000m, course: FoodCourse.APPETIZER, marketId: marketId);
        var b = Eligible("Bún thịt", 55_000m, course: FoodCourse.MAIN_COURSE, marketId: marketId);
        var c = Eligible("Trà tắc", 20_000m, course: FoodCourse.DRINK, marketId: marketId);
        SetupPipeline(
            MealIntent(partySize: 2, budget: 200_000m, goal: "thử nhiều món đáng tiền", prefs: ["variety", "worth the money"]),
            [a, b, c],
            stageC: PlansJson(items:
            [
                (a.FoodItem.Id, 1, "APPETIZER"),
                (b.FoodItem.Id, 1, "MAIN_COURSE"),
                (c.FoodItem.Id, 2, "DRINK")
            ]));

        var result = await Send("Muốn thử nhiều món, bữa tối đáng đồng tiền.");

        var plan = Assert.Single(result.Data!.MealPlans);
        Assert.Equal(120_000m, plan.TotalPrice);
        Assert.Contains(plan.Sections, section => section.Course == "APPETIZER" && section.Items.Count == 1);
        Assert.Contains(plan.Items, item => item.FoodItemId == b.FoodItem.Id);
    }

    [Fact]
    public async Task MealPlan_StageCExtraId_Throws503()
    {
        var food = Eligible("Phở", 50_000m, course: FoodCourse.MAIN_COURSE);
        SetupPipeline(
            MealIntent(partySize: 1, budget: 80_000m),
            [food],
            stageC: PlansJson(items: [(Guid.NewGuid(), 1, "MAIN_COURSE")]));

        var exception = await Assert.ThrowsAsync<AppException>(() => Send("Soạn thực đơn 1 người."));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
    }

    [Fact]
    public async Task MealPlan_OverBudgetProposal_BackendTotalIsAuthoritative()
    {
        var main = Eligible("Hải sản", 80_000m, course: FoodCourse.MAIN_COURSE);
        SetupPipeline(
            MealIntent(partySize: 1, budget: 90_000m),
            [main],
            stageC: """
                { "plans": [ { "title": null, "items": [ { "foodItemId": "PLACEHOLDER", "quantity": 2, "course": "MAIN_COURSE" } ] } ] }
                """.Replace("PLACEHOLDER", main.FoodItem.Id.ToString()));

        var result = await Send("Thực đơn tối đa 90k.");

        var plan = Assert.Single(result.Data!.MealPlans);
        Assert.Equal(80_000m, plan.TotalPrice);
        Assert.Equal(80_000m, plan.EstimatedTotal);
        Assert.Equal(10_000m, plan.RemainingBudget);
        Assert.Equal(1, plan.Items[0].Quantity);
        Assert.Contains("BUDGET_QTY_REDUCED", plan.Warnings);
    }

    [Fact]
    public async Task MealPlan_ThreeOptionsConfigured_ReturnsOneWhenOnlyOneQuality()
    {
        var food = Eligible("Bún riêu", 45_000m, course: FoodCourse.MAIN_COURSE);
        var id = food.FoodItem.Id;
        SetupPipeline(
            MealIntent(partySize: 1, budget: 80_000m),
            [food],
            stageC: $$"""
                {
                  "plans": [
                    { "items": [ { "foodItemId": "{{id}}", "quantity": 1, "course": "MAIN_COURSE" } ] },
                    { "items": [ { "foodItemId": "{{id}}", "quantity": 1, "course": "MAIN_COURSE" } ] },
                    { "items": [ { "foodItemId": "{{id}}", "quantity": 1, "course": "MAIN_COURSE" } ] }
                  ]
                }
                """);

        var result = await Send("Cho mình vài thực đơn.");

        Assert.Equal(3, _options.MaxMealPlanOptions);
        Assert.Single(result.Data!.MealPlans);
        Assert.Equal(result.Data.MealPlan!.Id, result.Data.MealPlans[0].Id);
    }

    [Fact]
    public async Task MealPlan_NoDessertInPool_DessertSectionEmpty()
    {
        var marketId = Guid.NewGuid();
        var appetizer = Eligible("Gỏi cuốn", 30_000m, course: FoodCourse.APPETIZER, marketId: marketId);
        var main = Eligible("Cơm gà", 55_000m, course: FoodCourse.MAIN_COURSE, marketId: marketId);
        var drink = Eligible("Nước sâm", 15_000m, course: FoodCourse.DRINK, marketId: marketId);
        SetupPipeline(
            MealIntent(partySize: 1, budget: 150_000m),
            [appetizer, main, drink],
            stageC: PlansJson(items:
            [
                (appetizer.FoodItem.Id, 1, "APPETIZER"),
                (main.FoodItem.Id, 1, "DESSERT"),
                (drink.FoodItem.Id, 1, "DRINK")
            ]));

        var result = await Send("Thực đơn đầy đủ các món.");

        var plan = Assert.Single(result.Data!.MealPlans);
        Assert.DoesNotContain(plan.Sections, section => section.Course == "DESSERT");
        Assert.DoesNotContain(plan.Items, item => item.Course == "DESSERT");
        Assert.Contains(plan.Items, item => item.FoodItemId == main.FoodItem.Id && item.Course == "MAIN_COURSE");
    }

    private Task<ApiResponse<ApplicationLayer.DTOs.Responses.AssistantTurnResponse>> Send(string message)
        => _service.SendMessageAsync(_conversation.CustomerId, _conversation.Id, new SendAssistantMessageRequest { Message = message });

    private void SetupPipeline(
        string stageA,
        IReadOnlyList<AssistantEligibleFood> eligible,
        Func<IReadOnlyList<Guid>, string, string>? stageB = null,
        string? stageC = null)
    {
        _foods.Setup(repository => repository.GetEligibleFoodsAsync(It.IsAny<AssistantFoodQueryCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantFoodQueryResult { Foods = eligible.ToArray(), Pipeline = new AssistantFoodQueryPipelineDiagnostics { AfterHardConstraints = eligible.Count } });
        _llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string system, string user, int _, CancellationToken _) =>
            {
                if (system.Contains("STAGE A", StringComparison.Ordinal))
                    return Task.FromResult(stageA);

                if (system.Contains("STAGE B", StringComparison.Ordinal))
                {
                    var ids = CandidateIds(user);
                    return Task.FromResult(stageB?.Invoke(ids, user) ?? ScoresJson(ids, 0.8, reasons: ["khớp"], unknown: []));
                }

                if (system.Contains("STAGE C", StringComparison.Ordinal))
                    return Task.FromResult(stageC ?? """{ "plans": [] }""");

                throw new InvalidOperationException(system);
            });
    }

    private static string RecommendIntent(string leftover)
        => $$"""
            {
              "intent": "FOOD_RECOMMENDATION", "needsLocation": false,
              "hardConstraints": {}, "structuredPreferences": {},
              "semanticPreferences": ["{{leftover}}"], "semanticAvoidances": [],
              "diningContext": "hôm nay", "userGoal": null
            }
            """;

    private static string MealIntent(int partySize, decimal budget, string? context = null, string? goal = null, string[]? prefs = null)
    {
        var semantic = JsonSerializer.Serialize(prefs ?? []);
        return $$"""
            {
              "intent": "MEAL_PLAN", "needsLocation": false, "partySize": {{partySize}}, "budgetMax": {{budget}},
              "hardConstraints": {}, "structuredPreferences": {},
              "semanticPreferences": {{semantic}}, "semanticAvoidances": [],
              "diningContext": {{ToJson(context)}}, "userGoal": {{ToJson(goal)}}
            }
            """;
    }

    private static string ToJson(string? value)
        => value is null ? "null" : JsonSerializer.Serialize(value);

    private static string ScoresJson(IReadOnlyList<Guid> ids, double score, string[] reasons, string[] unknown)
        => JsonSerializer.Serialize(new
        {
            scores = ids.Select(id => new
            {
                foodItemId = id,
                semanticCompatibility = score,
                reasons,
                unknownDataFacets = unknown
            }).ToArray()
        });

    private static string PlansJson(string? title = null, string? overall = null, params (Guid Id, int Qty, string Course)[] items)
        => JsonSerializer.Serialize(new
        {
            plans = new[]
            {
                new
                {
                    title,
                    overallReason = overall,
                    items = items.Select(item => new
                    {
                        foodItemId = item.Id,
                        quantity = item.Qty,
                        course = item.Course
                    }).ToArray()
                }
            }
        });

    private static IReadOnlyList<Guid> CandidateIds(string user)
    {
        using var document = JsonDocument.Parse(user);
        return document.RootElement.GetProperty("candidates")
            .EnumerateArray()
            .Select(item => item.GetProperty("foodItemId").GetGuid())
            .ToArray();
    }

    private static AssistantEligibleFood Eligible(
        string name,
        decimal price,
        FoodCourse? course = null,
        string? prep = null,
        bool shareable = false,
        int? serving = null,
        decimal rating = 0,
        int reviews = 0,
        bool featured = false,
        int soldToday = 0,
        int orderCount = 0,
        Guid? marketId = null)
    {
        var market = new NightMarket
        {
            Id = marketId ?? Guid.NewGuid(),
            Name = "Chợ Test",
            Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active,
            OpeningHours = new TimeOnly(0, 0),
            ClosingHours = new TimeOnly(0, 0)
        };
        var booth = new Booth { Id = Guid.NewGuid(), BoothName = "Quầy A", NightMarket = market, NightMarketId = market.Id, Status = BoothStatus.Active };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            Price = price,
            IsAvailable = true,
            IsFeatured = featured,
            AverageRating = rating,
            ReviewCount = reviews,
            Booth = booth,
            BoothId = booth.Id,
            Category = new FoodCategory { Id = Guid.NewGuid(), Name = "Món", Code = "MAIN" },
            IsShareable = shareable,
            EstimatedServingCount = serving,
            Ingredients = [],
            Allergens = [],
            DietaryAttributes = [],
            TasteProfiles = [],
            PreparationMethods = [],
            Courses = []
        };
        if (course.HasValue)
            food.Courses.Add(new FoodItemCourse { FoodItemId = food.Id, Course = course.Value, IsPrimary = true });
        if (prep is not null)
        {
            var method = new PreparationMethod { Id = Guid.NewGuid(), Code = prep, Name = prep, IsActive = true };
            food.PreparationMethods.Add(new FoodItemPreparationMethod { FoodItemId = food.Id, PreparationMethod = method, PreparationMethodId = method.Id });
        }

        return new AssistantEligibleFood
        {
            FoodItem = food,
            EffectivePrice = price,
            SoldToday = soldToday,
            OrderCount = orderCount
        };
    }

    private static FoodSemanticCatalogSet Catalog()
        => new(
            [new Ingredient { Id = Guid.NewGuid(), Code = "PORK", Name = "Pork", IsActive = true }],
            [new Allergen { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Code = "CRUSTACEAN", Name = "Crustacean", IsActive = true }],
            [],
            [new PreparationMethod { Id = Guid.NewGuid(), Code = "GRILLED", Name = "Grilled", IsActive = true }],
            [new TasteProfile { Id = Guid.NewGuid(), Code = "SPICY", Name = "Spicy", IsActive = true }]);
}
