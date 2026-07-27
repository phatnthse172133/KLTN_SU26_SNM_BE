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
        var settingsRepository = new Mock<IGenericRepository<SystemSetting>>();
        settingsRepository.Setup(repository => repository.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<SystemSetting, bool>>>()))
            .ReturnsAsync([]);
        var provider = new GeminiAIProviderService(
            new HttpClient(),
            Options.Create(new AIProviderSettings { EnableExternalProvider = false }),
            settingsRepository.Object);

        var intent = await provider.ParseFoodIntentAsync("Đi 4 người với ngân sách 300k", []);

        Assert.Equal(300_000m, intent.BudgetMax);
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
        Assert.Equal(90_000m, item.Price);
        Assert.Equal(100_000m, result.Data.ParsedIntent.BudgetMax);
    }

    [Fact]
    public async Task DiningPlan_RepairsQuantitiesAndNeverExceedsStrictBudget()
    {
        var main = Tag("FULLMEAL");
        var drink = Tag("DRINK");
        var mainFood = Food(80_000m, main);
        var drinkFood = Food(50_000m, drink, mainFood.Booth.NightMarket, mainFood.Booth);
        var service = CreateService([main, drink], [mainFood, drinkFood], new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = mainFood.Booth.NightMarketId,
            GroupSize = 2,
            Budget = 100_000m
        });

        var option = Assert.Single(result.Data!.Options);
        Assert.True(option.EstimatedTotal <= option.Budget);
        Assert.Equal(option.Budget - option.EstimatedTotal, option.RemainingBudget);
        Assert.All(option.PlanPreview, item =>
            Assert.Equal(option.NightMarketId, new[] { mainFood, drinkFood }.Single(f => f.Id == item.FoodItemId).Booth.NightMarketId));
    }

    [Fact]
    public async Task DiningStyleLightMeal_UsesSupportedLightTags()
    {
        var mild = Tag("MILD");
        var full = Tag("FULLMEAL");
        var fullFood = Food(50_000m, full);
        var lightFood = Food(50_000m, mild, fullFood.Booth.NightMarket, fullFood.Booth);
        var service = CreateService([mild, full], [fullFood, lightFood], new FoodIntentDto());

        var result = await service.DiningPlanAssistantAsync(Guid.NewGuid(), new DiningPlanAssistantRequest
        {
            NightMarketId = fullFood.Booth.NightMarketId,
            DiningStyle = "LightMeal",
            GroupSize = 1,
            Budget = 100_000m
        });

        var option = Assert.Single(result.Data!.Options);
        var item = Assert.Single(option.PlanPreview);
        Assert.Equal(lightFood.Id, item.FoodItemId);
        Assert.Equal("LightMeal", item.Role);
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
}
