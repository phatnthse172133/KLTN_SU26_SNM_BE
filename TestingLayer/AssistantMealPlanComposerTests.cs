using ApplicationLayer.Exceptions;
using ApplicationLayer.Services.Assistant;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.Extensions.Options;
using Moq;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class AssistantMealPlanComposerTests
{
    [Fact]
    public void ResolveQuantity_ShareableLargeServing_IsOne()
    {
        var food = Food("Lẩu", shareable: true, serving: 4);
        Assert.Equal(1, AssistantMealPlanComposer.ResolveQuantity(food, partySize: 3));
    }

    [Fact]
    public async Task ComposeAsync_EmptyRanked_DoesNotCallProvider()
    {
        var llm = new Mock<ILanguageModelClient>();
        var options = Options.Create(new AssistantOptions());
        var composer = new AssistantMealPlanComposer(
            llm.Object,
            new AssistantMealPlanValidator(options),
            options,
            Options.Create(new OpenAiOptions { ApiKey = "test" }));

        var plans = await composer.ComposeAsync(
            "bữa ăn nhóm",
            new ParsedAssistantIntent { Intent = AssistantIntentKind.MEAL_PLAN },
            [],
            CancellationToken.None);

        Assert.Empty(plans);
        llm.Verify(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<LanguageModelJsonSchemaOptions>()), Times.Never);
    }

    [Fact]
    public void ParseProposals_UnknownFoodItemId_Throws503()
    {
        var allowed = Guid.NewGuid();
        var extra = Guid.NewGuid();
        var raw = $$"""
            {
              "plans": [
                {
                  "items": [
                    { "foodItemId": "{{allowed}}", "quantity": 1, "course": "MAIN_COURSE" },
                    { "foodItemId": "{{extra}}", "quantity": 1, "course": "DRINK" }
                  ]
                }
              ]
            }
            """;

        var exception = Assert.Throws<AppException>(() =>
            AssistantMealPlanComposer.ParseProposals(raw, new HashSet<Guid> { allowed }));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
        Assert.Equal(AssistantErrors.HallucinatedId, AssistantProviderFailure.Reason(exception));
        Assert.Null(AssistantProviderFailure.ParseFailureCategory(exception));
    }

    [Fact]
    public void ParseProposals_TruncatedJson_ThrowsInvalidJson()
    {
        var exception = Assert.Throws<AppException>(() =>
            AssistantMealPlanComposer.ParseProposals(
                new LanguageModelJsonCompletion
                {
                    Content = """{"plans":[{"title":"Tối nay","items":[""",
                    FinishReason = "length",
                    ConfiguredMaxOutputTokens = 400,
                    OutputTokenCount = 400,
                    ResponseCharacterCount = 40
                },
                new HashSet<Guid> { Guid.NewGuid() }));

        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantJsonParseClassifier.OutputTruncated, AssistantProviderFailure.ParseFailureCategory(exception));
        Assert.Equal(AssistantLlmStages.MealPlan, AssistantProviderFailure.Stage(exception));
    }

    [Fact]
    public void ParseProposals_Malformed_DoesNotInventPlan()
    {
        var exception = Assert.Throws<AppException>(() =>
            AssistantMealPlanComposer.ParseProposals("nope", new HashSet<Guid> { Guid.NewGuid() }));

        Assert.Equal(AssistantErrors.InvalidJson, AssistantProviderFailure.Reason(exception));
        Assert.Equal(AssistantJsonParseClassifier.MalformedJson, AssistantProviderFailure.ParseFailureCategory(exception));
    }

    [Fact]
    public void Validate_OverBudgetProposal_RecomputesWithBackendPrices()
    {
        var market = Guid.NewGuid();
        var expensive = Ranked("Cơm", 80_000m, market, FoodCourse.MAIN_COURSE, 0.9);
        var drink = Ranked("Trà", 20_000m, market, FoodCourse.DRINK, 0.8);
        var validator = new AssistantMealPlanValidator(Options.Create(new AssistantOptions { MaxMealPlanOptions = 3, MaxMealPlanItemsPerPlan = 8 }));

        var plans = validator.Validate(
            [
                new AssistantMealPlanProposal
                {
                    Title = "AI hypothetial cheap total",
                    Items =
                    [
                        new() { FoodItemId = expensive.Eligible.FoodItem.Id, Quantity = 2, Course = "MAIN" },
                        new() { FoodItemId = drink.Eligible.FoodItem.Id, Quantity = 1, Course = "DRINK" }
                    ]
                }
            ],
            [expensive, drink],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.MEAL_PLAN, PartySize = 1, BudgetMax = 100_000m });

        var plan = Assert.Single(plans);
        Assert.Equal(100_000m, plan.EstimatedTotal);
        Assert.Equal(0m, plan.RemainingBudget);
        Assert.Equal(80_000m, plan.Items.Single(item => item.FoodItem.Name == "Cơm").UnitPrice);
        Assert.Contains("BUDGET_QTY_REDUCED", plan.Warnings);
        Assert.NotEqual(40_000m, plan.EstimatedTotal);
    }

    [Fact]
    public void Validate_MaxOptionsThree_ReturnsOnlyDistinctQualityPlans()
    {
        var market = Guid.NewGuid();
        var main = Ranked("Bún", 40_000m, market, FoodCourse.MAIN_COURSE, 0.9);
        var drink = Ranked("Nước", 15_000m, market, FoodCourse.DRINK, 0.7);
        var dessert = Ranked("Chè", 25_000m, market, FoodCourse.DESSERT, 0.6);
        var validator = new AssistantMealPlanValidator(Options.Create(new AssistantOptions { MaxMealPlanOptions = 3, MaxMealPlanItemsPerPlan = 8 }));

        var plans = validator.Validate(
            [
                new AssistantMealPlanProposal
                {
                    Items =
                    [
                        new() { FoodItemId = main.Eligible.FoodItem.Id, Quantity = 1, Course = "MAIN_COURSE" },
                        new() { FoodItemId = drink.Eligible.FoodItem.Id, Quantity = 1, Course = "DRINK" }
                    ]
                },
                new AssistantMealPlanProposal
                {
                    Items =
                    [
                        new() { FoodItemId = drink.Eligible.FoodItem.Id, Quantity = 1, Course = "DRINK" },
                        new() { FoodItemId = main.Eligible.FoodItem.Id, Quantity = 1, Course = "MAIN_COURSE" }
                    ]
                },
                new AssistantMealPlanProposal
                {
                    Items =
                    [
                        new() { FoodItemId = dessert.Eligible.FoodItem.Id, Quantity = 1, Course = "DESSERT" }
                    ]
                }
            ],
            [main, drink, dessert],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.MEAL_PLAN, PartySize = 2 });

        Assert.Equal(2, plans.Count);
        Assert.Contains(plans, plan => plan.Items.Any(item => item.FoodItem.Name == "Bún"));
        Assert.Contains(plans, plan => plan.Items.Count == 1 && plan.Items[0].FoodItem.Name == "Chè");
    }

    [Fact]
    public void Validate_NoDessertInPool_DoesNotPadDessertWithMains()
    {
        var market = Guid.NewGuid();
        var main = Ranked("Bánh mì", 30_000m, market, FoodCourse.MAIN_COURSE, 0.9);
        var drink = Ranked("Sữa chua", 20_000m, market, FoodCourse.DRINK, 0.8);
        var validator = new AssistantMealPlanValidator(Options.Create(new AssistantOptions { MaxMealPlanOptions = 3 }));

        var plans = validator.Validate(
            [
                new AssistantMealPlanProposal
                {
                    Items =
                    [
                        new() { FoodItemId = main.Eligible.FoodItem.Id, Quantity = 1, Course = "DESSERT" },
                        new() { FoodItemId = drink.Eligible.FoodItem.Id, Quantity = 1, Course = "DRINK" }
                    ]
                }
            ],
            [main, drink],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.MEAL_PLAN, PartySize = 1 });

        var plan = Assert.Single(plans);
        Assert.DoesNotContain(plan.Items, item => item.Course == FoodCourse.DESSERT);
        Assert.Contains(plan.Items, item => item.Course == FoodCourse.MAIN_COURSE && item.FoodItem.Name == "Bánh mì");
        Assert.Contains(AssistantMealPlanValidator.SectionOrder(plan.Items.Select(item => item.Course)), course => course == FoodCourse.DESSERT);
    }

    [Fact]
    public void Validate_EmptyProposalItems_ReturnsNoPlan()
    {
        var validator = new AssistantMealPlanValidator(Options.Create(new AssistantOptions()));
        var plans = validator.Validate(
            [new AssistantMealPlanProposal { Items = [] }],
            [Ranked("Phở", 40_000m, Guid.NewGuid(), FoodCourse.MAIN_COURSE, 0.9)],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.MEAL_PLAN });
        Assert.Empty(plans);
    }

    private static AssistantScoredFood Ranked(string name, decimal price, Guid marketId, FoodCourse course, double score)
    {
        var food = Food(name, marketId: marketId, course: course);
        return new AssistantScoredFood
        {
            Eligible = new AssistantEligibleFood { FoodItem = food, EffectivePrice = price },
            FinalScore = score,
            SemanticScore = score
        };
    }

    private static FoodItem Food(string name, bool shareable = false, int? serving = null, Guid? marketId = null, FoodCourse? course = null)
    {
        var market = new NightMarket { Id = marketId ?? Guid.NewGuid(), Name = "Chợ A", Status = NightMarketStatus.Active };
        var booth = new Booth { Id = Guid.NewGuid(), BoothName = "Quầy", NightMarket = market, NightMarketId = market.Id };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            Booth = booth,
            BoothId = booth.Id,
            Category = new FoodCategory { Name = "Món", Code = "MAIN" },
            IsShareable = shareable,
            EstimatedServingCount = serving,
            Courses = []
        };
        if (course.HasValue)
        {
            food.Courses.Add(new FoodItemCourse { FoodItemId = food.Id, Course = course.Value, IsPrimary = true });
        }
        return food;
    }
}
