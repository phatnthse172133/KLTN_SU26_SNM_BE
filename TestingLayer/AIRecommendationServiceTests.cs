using ApplicationLayer.AI;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using ApplicationLayer.Exceptions;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Options;
using Moq;
using InfrastructureLayer.Cores.AI;
using static DomainLayer.Enums.GeneralEnum;
using System.Text.Json;

namespace TestingLayer;

public class AIRecommendationServiceTests
{
    [Fact]
    public async Task SavedPreferenceOnly_RanksMatchingFoodFirst()
    {
        var spicy = Tag("SPICY");
        var mild = Tag("MILD");
        var spicyFood = Food(50_000m, spicy);
        var mildFood = Food(50_000m, mild);
        var saved = new CustomerPreference { FoodTagId = spicy.Id, FoodTag = spicy, PreferenceKind = CustomerPreferenceKind.Like };
        var service = CreateService([spicy, mild], [mildFood, spicyFood], new FoodIntentDto(), savedPreferences: [saved]);

        var result = await service.GetPersonalizedRecommendationsAsync(Guid.NewGuid());

        Assert.Equal(spicyFood.Id, result.Data!.Results.First().FoodItemId);
    }

    [Fact]
    public async Task HistoryCategoryAffinity_AffectsOrdering()
    {
        var tag = Tag("MILD");
        var preferredCategory = Guid.NewGuid();
        var other = Food(50_000m, tag);
        var preferred = Food(50_000m, tag);
        preferred.CategoryId = preferredCategory;
        preferred.Category.Id = preferredCategory;
        var context = Context(categories: new Dictionary<Guid, int> { [preferredCategory] = 10 });
        var service = CreateService([tag], [other, preferred], new FoodIntentDto(), context);

        var result = await service.GetPersonalizedRecommendationsAsync(Guid.NewGuid());

        Assert.Equal(preferred.Id, result.Data!.Results.First().FoodItemId);
    }

    [Fact]
    public async Task ExplicitCurrentPreference_OutranksHistoricalTag()
    {
        var sweet = Tag("SWEET");
        var spicy = Tag("SPICY");
        var spicyFood = Food(50_000m, spicy);
        var sweetFood = Food(50_000m, sweet);
        var context = Context(tags: new Dictionary<Guid, int> { [spicy.Id] = 100 });
        var service = CreateService([sweet, spicy], [spicyFood, sweetFood], new FoodIntentDto(), context);

        var result = await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest
        {
            SelectedTagIds = [sweet.Id]
        });

        Assert.Equal(sweetFood.Id, result.Data!.Results.First().FoodItemId);
    }

    [Fact]
    public async Task ExplicitAvoid_ExcludesHistoricalFavorite()
    {
        var spicy = Tag("SPICY");
        var mild = Tag("MILD");
        var spicyFood = Food(50_000m, spicy);
        var mildFood = Food(50_000m, mild);
        var avoid = new CustomerPreference { FoodTagId = spicy.Id, FoodTag = spicy, PreferenceKind = CustomerPreferenceKind.Avoid };
        var context = Context(tags: new Dictionary<Guid, int> { [spicy.Id] = 100 });
        var service = CreateService([spicy, mild], [spicyFood, mildFood], new FoodIntentDto(), context, [avoid]);

        var result = await service.GetPersonalizedRecommendationsAsync(Guid.NewGuid());

        Assert.DoesNotContain(result.Data!.Results, item => item.FoodItemId == spicyFood.Id);
    }

    [Fact]
    public async Task NegativeReview_DoesNotBoostBooth()
    {
        var tag = Tag("MILD");
        var negative = Food(50_000m, tag);
        var neutral = Food(50_000m, tag);
        var context = Context(negativeBooths: [negative.BoothId]);
        var service = CreateService([tag], [negative, neutral], new FoodIntentDto(), context);

        var result = await service.GetPersonalizedRecommendationsAsync(Guid.NewGuid());

        Assert.Equal(neutral.Id, result.Data!.Results.First().FoodItemId);
    }

    [Fact]
    public async Task NewSimilarFood_OutranksRecentlyOrderedFood()
    {
        var tag = Tag("GRILLED");
        var recent = Food(50_000m, tag);
        var novel = Food(50_000m, tag);
        var context = Context(tags: new Dictionary<Guid, int> { [tag.Id] = 5 }, recentFoods: [recent.Id]);
        var service = CreateService([tag], [recent, novel], new FoodIntentDto(), context);

        var result = await service.GetPersonalizedRecommendationsAsync(Guid.NewGuid());

        Assert.Equal(novel.Id, result.Data!.Results.First().FoodItemId);
    }

    [Fact]
    public async Task PreferNearMe_RanksNearerSuitableMarketFirst()
    {
        var tag = Tag("MILD");
        var farMarket = Market(10.85m, 106.80m);
        var nearMarket = Market(10.7722m, 106.6984m);
        var far = Food(50_000m, tag, farMarket);
        var near = Food(50_000m, tag, nearMarket);
        var service = CreateService([tag], [far, near], new FoodIntentDto());

        var result = await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest
        {
            PreferNearMe = true,
            Latitude = 10.7721m,
            Longitude = 106.6983m
        });

        Assert.Equal(near.Id, result.Data!.Results.First().FoodItemId);
        Assert.NotNull(result.Data.Results.First().DistanceMeters);
    }

    [Fact]
    public async Task PreferNearMeFalse_DoesNotChangeRankingOrReturnDistance()
    {
        var tag = Tag("MILD");
        var far = Food(50_000m, tag, Market(10.85m, 106.80m));
        var near = Food(50_000m, tag, Market(10.7722m, 106.6984m));
        var service = CreateService([tag], [far, near], new FoodIntentDto());

        var result = await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest
        {
            PreferNearMe = false,
            Latitude = 10.7721m,
            Longitude = 106.6983m
        });

        Assert.Equal(far.Id, result.Data!.Results.First().FoodItemId);
        Assert.All(result.Data.Results, item => Assert.Null(item.DistanceMeters));
    }

    [Theory]
    [InlineData(91, 106, "AI_LATITUDE_INVALID")]
    [InlineData(10, 181, "AI_LONGITUDE_INVALID")]
    public async Task InvalidCoordinates_ReturnStableValidationError(decimal latitude, decimal longitude, string errorCode)
    {
        var service = CreateService([], [], new FoodIntentDto());

        var exception = await Assert.ThrowsAsync<AppException>(() => service.FoodDiscoveryAsync(
            Guid.NewGuid(),
            new FoodDiscoveryRequest { PreferNearMe = true, Latitude = latitude, Longitude = longitude }));

        Assert.Equal(errorCode, exception.ErrorCode);
    }

    [Fact]
    public async Task PreferNearMeWithoutCoordinates_ReturnsExplicitError()
    {
        var service = CreateService([], [], new FoodIntentDto());

        var exception = await Assert.ThrowsAsync<AppException>(() => service.FoodDiscoveryAsync(
            Guid.NewGuid(), new FoodDiscoveryRequest { PreferNearMe = true }));

        Assert.Equal("AI_LOCATION_REQUIRED", exception.ErrorCode);
    }

    [Fact]
    public async Task SelectedMarket_RemainsHardConstraintAndDisablesGpsDistance()
    {
        var tag = Tag("MILD");
        var selectedMarket = Market(11m, 107m);
        var otherMarket = Market(10.7722m, 106.6984m);
        var selectedFood = Food(50_000m, tag, selectedMarket);
        var closerOtherFood = Food(50_000m, tag, otherMarket);
        var service = CreateService([tag], [closerOtherFood, selectedFood], new FoodIntentDto());

        var result = await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest
        {
            NightMarketId = selectedMarket.Id,
            PreferNearMe = true,
            Latitude = 10.7721m,
            Longitude = 106.6983m
        });

        var item = Assert.Single(result.Data!.Results);
        Assert.Equal(selectedFood.Id, item.FoodItemId);
        Assert.Null(item.DistanceMeters);
    }

    [Fact]
    public async Task NearMe_DoesNotPersistPreciseCoordinatesOrDerivedDistance()
    {
        var tag = Tag("MILD");
        var food = Food(50_000m, tag, Market(10.7722m, 106.6984m));
        AIRecommendationLog? savedLog = null;
        var logs = new Mock<IAIRecommendationLogRepository>();
        logs.Setup(repository => repository.AddAsync(It.IsAny<AIRecommendationLog>()))
            .Callback<AIRecommendationLog>(log => savedLog = log)
            .Returns(Task.CompletedTask);
        logs.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);
        var service = CreateService([tag], [food], new FoodIntentDto(), logRepository: logs);

        await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest
        {
            PreferNearMe = true,
            Latitude = 10.7721m,
            Longitude = 106.6983m
        });

        Assert.NotNull(savedLog);
        Assert.DoesNotContain("10.7721", savedLog.InputJson);
        Assert.DoesNotContain("106.6983", savedLog.InputJson);
        Assert.Contains("\"distanceMeters\":null", savedLog.ResultJson);
    }

    [Fact]
    public async Task LocalIntentParser_DoesNotMergeGroupSizeIntoBudget()
    {
        var provider = new LocalOnlyAIProviderService();

        var intent = await provider.ParseFoodIntentAsync("Đi 4 người với ngân sách 300k", []);

        Assert.Equal(300_000m, intent.BudgetMax);
    }

    [Fact]
    public async Task LocalIntentParser_CurrentNotSpicyOverridesSpicyKeyword()
    {
        var provider = new LocalOnlyAIProviderService();

        var intent = await provider.ParseFoodIntentAsync("Hôm nay tôi muốn món không cay", ["SPICY", "MILD"]);

        Assert.DoesNotContain("SPICY", intent.MatchedTagNames);
        Assert.Contains("SPICY", intent.AvoidTagNames);
    }

    [Fact]
    public async Task FoodDiscovery_UsesEffectivePriceAndHonorsStructuredBudget()
    {
        var spicy = Tag("SPICY");
        var affordable = Food(90_000m, spicy);
        var effectivePriceTooHigh = Food(50_000m, spicy);
        effectivePriceTooHigh.FoodPrices.Add(new FoodPrice
        {
            Id = Guid.NewGuid(),
            Price = 120_000m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });

        var providerIntent = new FoodIntentDto
        {
            MatchedTagNames = ["SPICY"],
            BudgetMax = 500_000m
        };
        var service = CreateService([spicy], [affordable, effectivePriceTooHigh], providerIntent);

        var result = await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest
        {
            SelectedTagIds = [spicy.Id],
            BudgetMax = 100_000m,
            Limit = 3
        });

        var item = Assert.Single(result.Data!.Results);
        Assert.Equal(affordable.Id, item.FoodItemId);
        Assert.Equal(90_000m, item.BasePrice);
        Assert.Equal(90_000m, item.EffectivePrice);
        Assert.Equal(90_000m, item.Price);
        Assert.Equal(100_000m, result.Data.ParsedIntent.BudgetMax);
    }

    [Fact]
    public async Task FoodDiscovery_NoDiscountReturnsEqualBaseEffectiveAndLegacyPrices()
    {
        var tag = Tag("MILD");
        var food = Food(50_000m, tag);
        var service = CreateService([tag], [food], new FoodIntentDto());

        var result = await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest());

        var item = Assert.Single(result.Data!.Results);
        Assert.Equal(50_000m, item.BasePrice);
        Assert.Equal(50_000m, item.EffectivePrice);
        Assert.Equal(item.EffectivePrice, item.Price);
    }

    [Fact]
    public async Task PersonalizedRecommendations_RealDiscountReturnsTruthfulPriceContract()
    {
        var tag = Tag("GRILLED");
        var food = Food(55_000m, tag);
        food.FoodPrices.Add(new FoodPrice
        {
            Id = Guid.NewGuid(),
            Price = 49_000m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        var service = CreateService([tag], [food], new FoodIntentDto());

        var result = await service.GetPersonalizedRecommendationsAsync(Guid.NewGuid());

        var item = Assert.Single(result.Data!.Results);
        Assert.Equal(55_000m, item.BasePrice);
        Assert.Equal(49_000m, item.EffectivePrice);
        Assert.Equal(item.EffectivePrice, item.Price);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(
            result.Data,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var serializedItem = json.RootElement.GetProperty("results")[0];
        Assert.Equal(55_000m, serializedItem.GetProperty("basePrice").GetDecimal());
        Assert.Equal(49_000m, serializedItem.GetProperty("effectivePrice").GetDecimal());
        Assert.Equal(49_000m, serializedItem.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task FoodDiscovery_PriceLowToHighOrderingStillUsesEffectivePrice()
    {
        var tag = Tag("MILD");
        var discounted = Food(60_000m, tag);
        discounted.FoodPrices.Add(new FoodPrice
        {
            Id = Guid.NewGuid(),
            Price = 40_000m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });
        var regular = Food(50_000m, tag);
        var service = CreateService([tag], [regular, discounted], new FoodIntentDto());

        var result = await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest
        {
            SortBy = "PriceLowToHigh",
            Limit = 3
        });

        Assert.Equal([discounted.Id, regular.Id], result.Data!.Results.Select(item => item.FoodItemId));
        Assert.Collection(
            result.Data.Results,
            item =>
            {
                Assert.Equal(60_000m, item.BasePrice);
                Assert.Equal(40_000m, item.EffectivePrice);
            },
            item =>
            {
                Assert.Equal(50_000m, item.BasePrice);
                Assert.Equal(50_000m, item.EffectivePrice);
            });
    }

    [Fact]
    public async Task DiningPlan_DoesNotReduceRequiredQuantitiesToForceAnUnderfundedMenu()
    {
        var appetizer = Tag("COURSE_APPETIZER");
        var main = Tag("COURSE_MAIN_COURSE");
        var drink = Tag("COURSE_DRINK");
        var dessert = Tag("COURSE_DESSERT");
        var market = Market(10.77m, 106.69m);
        var foods = new[]
        {
            Food(30_000m, appetizer, market), Food(80_000m, main, market),
            Food(20_000m, drink, market), Food(30_000m, dessert, market)
        };
        var service = CreateService([appetizer, main, drink, dessert], foods, new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = market.Id,
            GroupSize = 2,
            Budget = 150_000m
        });

        Assert.Empty(result.Data!.Options);
        Assert.Equal("NO_PLAN_FOUND", result.Data.Step);
        Assert.Empty(result.Data.MissingRequiredCourses);
        Assert.Equal(260_000m, result.Data.MinimumRequiredBudget);
    }

    [Fact]
    public async Task DiningPlan_DoesNotReturnSingleFoodAsAGroupPlan()
    {
        var main = Tag("FULLMEAL");
        var onlyFood = Food(50_000m, main);
        var service = CreateService([main], [onlyFood], new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = onlyFood.Booth.NightMarketId,
            GroupSize = 3,
            Budget = 500_000m
        });

        Assert.Empty(result.Data!.Options);
        Assert.Equal("NO_PLAN_FOUND", result.Data.Step);
    }

    [Fact]
    public async Task DiningPlan_UsesCanonicalTaxonomyToBuildMultipleCoursesForThreePeople()
    {
        var appetizer = Tag("COURSE_APPETIZER");
        var main = Tag("COURSE_MAIN_COURSE");
        var drink = Tag("COURSE_DRINK");
        var dessert = Tag("COURSE_DESSERT");
        var market = Market(10.77m, 106.69m);
        var appetizerFood = Food(25_000m, appetizer, market);
        var mainFood = Food(60_000m, main, market);
        var drinkFood = Food(20_000m, drink, market);
        var dessertFood = Food(30_000m, dessert, market);
        var service = CreateService([appetizer, main, drink, dessert],
            [appetizerFood, mainFood, drinkFood, dessertFood], new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = mainFood.Booth.NightMarketId,
            GroupSize = 3,
            Budget = 350_000m,
            DiningStyle = "FullMeal"
        });

        var option = result.Data!.Options.Single(value => value.OptionType == "BestMatch");
        Assert.True(option.IsCompleteMenu);
        Assert.Collection(option.PlanPreview,
            item =>
            {
                Assert.Equal(appetizerFood.Id, item.FoodItemId);
                Assert.Equal("APPETIZER", item.Course);
                Assert.Equal(2, item.Quantity);
            },
            item =>
            {
                Assert.Equal(mainFood.Id, item.FoodItemId);
                Assert.Equal("MAIN_COURSE", item.Course);
                Assert.Equal(3, item.Quantity);
            },
            item =>
            {
                Assert.Equal(drinkFood.Id, item.FoodItemId);
                Assert.Equal("DRINK", item.Course);
                Assert.Equal(3, item.Quantity);
            },
            item =>
            {
                Assert.Equal(dessertFood.Id, item.FoodItemId);
                Assert.Equal("DESSERT", item.Course);
                Assert.Equal(2, item.Quantity);
            });
        Assert.Equal(option.PlanPreview.Sum(item => item.TotalPrice), option.EstimatedTotal);
        Assert.Equal(option.Budget - option.EstimatedTotal, option.RemainingBudget);
    }

    [Fact]
    public async Task DiningPlan_AddsOptionalSideDishWhenItFitsAndPreservesCourseOrder()
    {
        var tags = new[]
        {
            Tag("COURSE_APPETIZER"), Tag("COURSE_MAIN_COURSE"), Tag("COURSE_SIDE_DISH"),
            Tag("COURSE_DRINK"), Tag("COURSE_DESSERT")
        };
        var market = Market(10.77m, 106.69m);
        var foods = tags.Select(tag => Food(20_000m, tag, market)).ToList();
        var service = CreateService(tags, foods, new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = market.Id, GroupSize = 2, Budget = 200_000m
        });

        var option = result.Data!.Options.Single(value => value.OptionType == "BestMatch");
        Assert.Equal(new[] { "APPETIZER", "MAIN_COURSE", "SIDE_DISH", "DRINK", "DESSERT" },
            option.PlanPreview.Select(item => item.Course));
        Assert.True(option.IsCompleteMenu);
    }

    [Fact]
    public async Task DiningPlan_ReportsMissingRequiredCourseInsteadOfRelabelingAnotherFood()
    {
        var tags = new[] { Tag("COURSE_APPETIZER"), Tag("COURSE_MAIN_COURSE"), Tag("COURSE_DRINK") };
        var market = Market(10.77m, 106.69m);
        var foods = tags.Select(tag => Food(20_000m, tag, market)).ToList();
        var service = CreateService(tags, foods, new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = market.Id, GroupSize = 3, Budget = 1_000_000m
        });

        Assert.Empty(result.Data!.Options);
        Assert.Equal(new[] { "DESSERT" }, result.Data.MissingRequiredCourses);
        Assert.Null(result.Data.MinimumRequiredBudget);
    }

    [Fact]
    public async Task DiningStyleLightMeal_StillReturnsACompleteOrderedMenu()
    {
        var courses = new[] { Tag("COURSE_APPETIZER"), Tag("COURSE_MAIN_COURSE"), Tag("COURSE_DRINK"), Tag("COURSE_DESSERT") };
        var market = Market(10.77m, 106.69m);
        var foods = courses.Select(tag => Food(20_000m, tag, market)).ToList();
        var service = CreateService(courses, foods, new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = market.Id,
            DiningStyle = "LightMeal",
            GroupSize = 1,
            Budget = 100_000m
        });

        var option = Assert.Single(result.Data!.Options);
        Assert.Equal(new[] { "APPETIZER", "MAIN_COURSE", "DRINK", "DESSERT" },
            option.PlanPreview.Select(item => item.Course));
    }

    [Fact]
    public async Task FoodDiscovery_ReturnsAtMostThreeAuthoritativeCandidates()
    {
        var tag = Tag("GRILLED");
        var foods = Enumerable.Range(0, 8).Select(index => Food(10_000m + index, tag)).ToList();
        var service = CreateService([tag], foods, new FoodIntentDto { MatchedTagNames = [tag.Code] });

        var result = await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest { Limit = 50 });

        Assert.Equal(3, result.Data!.Results.Count);
        Assert.All(result.Data.Results, item => Assert.Contains(foods, food => food.Id == item.FoodItemId));
    }

    [Fact]
    public async Task CurrentPreference_IsStrongerThanSavedLike()
    {
        var spicy = Tag("SPICY");
        var mild = Tag("MILD");
        var spicyFood = Food(50_000m, spicy);
        var mildFood = Food(50_000m, mild);
        var saved = new CustomerPreference
        {
            FoodTagId = spicy.Id, FoodTag = spicy, PreferenceKind = CustomerPreferenceKind.Like
        };
        var service = CreateService([spicy, mild], [spicyFood, mildFood], new FoodIntentDto(), savedPreferences: [saved]);

        var result = await service.FoodDiscoveryAsync(Guid.NewGuid(), new FoodDiscoveryRequest
        {
            SelectedTagIds = [mild.Id]
        });

        Assert.Equal(mildFood.Id, result.Data!.Results.First().FoodItemId);
    }

    [Fact]
    public async Task FoodDiscovery_NoEligibleFoodReturnsStableErrorCode()
    {
        var service = CreateService([], [], new FoodIntentDto());

        var error = await Assert.ThrowsAsync<AppException>(() => service.FoodDiscoveryAsync(
            Guid.NewGuid(), new FoodDiscoveryRequest { BudgetMax = 50_000m }));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal(AIErrorCodes.NoCandidates, error.ErrorCode);
    }

    [Fact]
    public async Task DiningPlan_ExcludesCandidateThatIsNotOrderableNow()
    {
        var main = Tag("FULLMEAL");
        var food = Food(50_000m, main);
        food.Booth.NightMarket.Status = NightMarketStatus.Closed;
        var service = CreateService([main], [food], new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = food.Booth.NightMarketId,
            GroupSize = 1,
            Budget = 100_000m
        });

        Assert.Equal("NO_PLAN_FOUND", result.Data!.Step);
        Assert.Empty(result.Data.Options);
        Assert.NotEqual(Guid.Empty, result.Data.LogId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task DiningPlan_GroupSizeOutsideOneToFiftyIsRejected(int groupSize)
    {
        var service = CreateService([], [], new FoodIntentDto());

        var error = await Assert.ThrowsAsync<AppException>(() => service.DiningPlanAssistantAsync(
            Guid.NewGuid(), new DiningPlanAssistantRequest { GroupSize = groupSize, Budget = 100_000m }));

        Assert.Equal(AIErrorCodes.InvalidRequest, error.ErrorCode);
    }

    [Fact]
    public async Task DiningPlan_StrategyLabelsRepresentDistinctAlgorithms()
    {
        var main = Tag("COURSE_MAIN_COURSE");
        var appetizer = Tag("COURSE_APPETIZER");
        var drink = Tag("COURSE_DRINK");
        var dessert = Tag("COURSE_DESSERT");
        var preferred = Tag("PREFERRED");
        var market = Market(10.77m, 106.69m);
        var best = Food(70_000m, main, market);
        best.Booth.AverageRating = 4m;
        AddTag(best, preferred);
        var cheap = Food(30_000m, main, market);
        cheap.Booth.AverageRating = 3m;
        var rated = Food(90_000m, main, market);
        rated.Booth.AverageRating = 5m;
        var starter = Food(10_000m, appetizer, market);
        var beverage = Food(10_000m, drink, market);
        var sweet = Food(10_000m, dessert, market);
        var service = CreateService([main, appetizer, drink, dessert, preferred],
            [cheap, rated, best, starter, beverage, sweet], new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = market.Id,
            PreferredTagIds = [preferred.Id],
            GroupSize = 1,
            Budget = 150_000m
        });

        var options = result.Data!.Options.ToDictionary(option => option.OptionType);
        Assert.Equal(best.Id, options["BestMatch"].PlanPreview.Single(item => item.Course == "MAIN_COURSE").FoodItemId);
        Assert.Equal(cheap.Id, options["BudgetFriendly"].PlanPreview.Single(item => item.Course == "MAIN_COURSE").FoodItemId);
        Assert.Equal(rated.Id, options["HighRating"].PlanPreview.Single(item => item.Course == "MAIN_COURSE").FoodItemId);
        Assert.All(options.Values, option => Assert.True(option.IsCompleteMenu));
        Assert.True(options["BudgetFriendly"].EstimatedTotal <= options["BestMatch"].EstimatedTotal);
        Assert.True(options["BudgetFriendly"].EstimatedTotal <= options["HighRating"].EstimatedTotal);
        Assert.Single(options.Values.Select(option => option.NightMarketId).Distinct());
    }

    [Fact]
    public async Task DiningPlan_LogFailureNeverReturnsConfirmableEmptyId()
    {
        var main = Tag("FULLMEAL");
        var food = Food(40_000m, main);
        var logs = new Mock<IAIRecommendationLogRepository>();
        logs.Setup(repository => repository.AddAsync(It.IsAny<AIRecommendationLog>())).Returns(Task.CompletedTask);
        logs.Setup(repository => repository.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException("database down"));
        var service = CreateService([main], [food], new FoodIntentDto(), logRepository: logs);

        var error = await Assert.ThrowsAsync<AppException>(() => service.DiningPlanAssistantAsync(
            Guid.NewGuid(), new DiningPlanAssistantRequest
            {
                NightMarketId = food.Booth.NightMarketId, GroupSize = 1, Budget = 100_000m
            }));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal(AIErrorCodes.PlanLogUnavailable, error.ErrorCode);
        Assert.DoesNotContain("database down", error.Message);
    }

    [Fact]
    public async Task Confirm_RevalidatesMarketAndReturnsStableErrorCode()
    {
        var appetizer = Tag("COURSE_APPETIZER");
        var main = Tag("COURSE_MAIN_COURSE");
        var drink = Tag("COURSE_DRINK");
        var dessert = Tag("COURSE_DESSERT");
        var market = Market(10.77m, 106.69m);
        var foods = new[]
        {
            Food(20_000m, appetizer, market), Food(40_000m, main, market),
            Food(15_000m, drink, market), Food(20_000m, dessert, market)
        };
        market.Status = NightMarketStatus.Closed;
        var customerId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var option = new DiningPlanOptionResponse
        {
            OptionId = "OPT_BEST_MATCH", NightMarketId = market.Id,
            NightMarketName = market.Name, GroupSize = 1, Budget = 100_000m,
            EstimatedTotal = foods.Sum(food => food.Price),
            PlanPreview =
            [
                PlanItem(foods[0], "APPETIZER"), PlanItem(foods[1], "MAIN_COURSE"),
                PlanItem(foods[2], "DRINK"), PlanItem(foods[3], "DESSERT")
            ]
        };
        var logs = new Mock<IAIRecommendationLogRepository>();
        logs.Setup(repository => repository.GetByIdAsync(logId)).ReturnsAsync(new AIRecommendationLog
        {
            Id = logId, CustomerId = customerId, RecommendationType = AIRecommendationType.DiningPlan,
            InputJson = "{}", ResultJson = JsonSerializer.Serialize(new DiningPlanAssistantResponse { Options = [option] })
        });
        var service = CreateService([appetizer, main, drink, dessert], foods, new FoodIntentDto(), logRepository: logs);

        var error = await Assert.ThrowsAsync<AppException>(() => service.ConfirmDiningPlanAsync(
            customerId, new ConfirmDiningPlanRequest { LogId = logId, OptionId = option.OptionId }));

        Assert.Equal(AIErrorCodes.MarketNotOrderable, error.ErrorCode);
    }

    private static AIRecommendationService CreateService(
        IReadOnlyCollection<FoodTag> tags,
        IReadOnlyCollection<FoodItem> foods,
        FoodIntentDto providerIntent,
        CustomerRecommendationContext? customerContext = null,
        IReadOnlyCollection<CustomerPreference>? savedPreferences = null,
        Mock<IAIRecommendationLogRepository>? logRepository = null)
    {
        var foodRepository = new Mock<IFoodItemRepository>();
        foodRepository.Setup(repository => repository.GetAiCandidatesAsync(
                It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(foods);
        foodRepository.Setup(repository => repository.GetAiOrderableCandidatesAsync(
                It.IsAny<Guid?>(), It.IsAny<TimeOnly>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(foods);
        foodRepository.Setup(repository => repository.GetAllFoodItemsByIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync((List<Guid> ids) => foods.Where(food => ids.Contains(food.Id)).ToList());

        var tagRepository = new Mock<IFoodTagRepository>();
        tagRepository.Setup(repository => repository.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        var preferences = new Mock<ICustomerPreferenceRepository>();
        preferences.Setup(repository => repository.GetByCustomerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedPreferences ?? []);

        var contextRepository = new Mock<IAICustomerContextRepository>();
        contextRepository.Setup(repository => repository.GetAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerContext ?? CustomerRecommendationContext.Empty);

        var provider = new Mock<IAIProviderService>();
        provider.Setup(service => service.ParseFoodIntentAsync(
                It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerIntent);

        return new AIRecommendationService(
            foodRepository.Object,
            tagRepository.Object,
            preferences.Object,
            (logRepository ?? new Mock<IAIRecommendationLogRepository>()).Object,
            provider.Object,
            contextRepository.Object,
            Options.Create(new AIProviderSettings()),
            TimeProvider.System);
    }

    private static FoodTag Tag(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        Status = FoodTagStatus.Active
    };

    private static CustomerRecommendationContext Context(
        IReadOnlyDictionary<Guid, int>? tags = null,
        IReadOnlyDictionary<Guid, int>? categories = null,
        IReadOnlyCollection<Guid>? recentFoods = null,
        IReadOnlyCollection<Guid>? negativeBooths = null)
        => new(
            tags ?? new Dictionary<Guid, int>(),
            categories ?? new Dictionary<Guid, int>(),
            new Dictionary<Guid, int>(),
            (recentFoods ?? []).ToHashSet(),
            new HashSet<Guid>(),
            (negativeBooths ?? []).ToHashSet(),
            null);

    private static NightMarket Market(decimal latitude, decimal longitude) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test market",
        Address = "Test",
        Latitude = latitude,
        Longitude = longitude,
        Status = NightMarketStatus.Open,
        ModerationStatus = ModerationStatus.Active,
        OpeningHours = new TimeOnly(0, 1),
        ClosingHours = new TimeOnly(23, 59)
    };

    private static FoodItem Food(decimal price, FoodTag tag, NightMarket? market = null, Booth? booth = null)
    {
        market ??= Market(10.7721m, 106.6983m);
        booth ??= new Booth
        {
            Id = Guid.NewGuid(),
            BoothName = "Test booth",
            NightMarketId = market.Id,
            NightMarket = market,
            Status = BoothStatus.Active,
            AverageRating = 4.5m
        };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            Name = "Test food",
            Price = price,
            IsAvailable = true,
            BoothId = booth.Id,
            Booth = booth,
            Category = new FoodCategory { Id = Guid.NewGuid(), Name = "Test category" }
        };
        food.FoodItemTags.Add(new FoodItemTag
        {
            FoodItemId = food.Id,
            FoodItem = food,
            FoodTagId = tag.Id,
            FoodTag = tag
        });
        return food;
    }

    private static void AddTag(FoodItem food, FoodTag tag)
        => food.FoodItemTags.Add(new FoodItemTag
        {
            FoodItemId = food.Id, FoodItem = food, FoodTagId = tag.Id, FoodTag = tag
        });

    private static DiningPlanItemResponse PlanItem(FoodItem food, string course, int quantity = 1)
        => new()
        {
            FoodItemId = food.Id,
            FoodName = food.Name,
            BoothId = food.BoothId,
            BoothName = food.Booth.BoothName,
            Quantity = quantity,
            UnitPrice = food.Price,
            TotalPrice = food.Price * quantity,
            Course = course,
            CourseDisplayName = course,
            CourseOrder = course switch
            {
                "APPETIZER" => 10, "MAIN_COURSE" => 20, "DRINK" => 60, "DESSERT" => 70, _ => 80
            },
            Role = course
        };
}
