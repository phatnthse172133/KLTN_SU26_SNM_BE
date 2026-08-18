using ApplicationLayer.Services.Assistant;
using ApplicationLayer.Services.CustomerDiscovery;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class AssistantDiscoveryEligibilityTests
{
    private static readonly DateTime ClosedHoursUtc = new(2026, 7, 27, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetEligibleFoods_AllMarketsOutsideHours_StillReturnsFoods()
    {
        await using var db = CreateContext();
        var food = await SeedFoodAsync(
            db,
            marketOpen: new TimeOnly(18, 0),
            marketClose: new TimeOnly(23, 0),
            boothOpen: null,
            boothClose: null,
            lat: 10.77m,
            lng: 106.69m);

        var repository = new AssistantFoodQueryRepository(db);
        var result = await repository.GetEligibleFoodsAsync(new AssistantFoodQueryCriteria
        {
            UtcNow = ClosedHoursUtc
        });

        var item = Assert.Single(result.Foods);
        Assert.Equal(food.Id, item.FoodItem.Id);
        Assert.True(result.Pipeline.AfterHardConstraints >= 1);
        Assert.Equal(0, result.Pipeline.AfterOpenNow);
    }

    [Fact]
    public async Task GetEligibleFoods_AllBoothsOutsideHours_StillReturnsFoods()
    {
        await using var db = CreateContext();
        var food = await SeedFoodAsync(
            db,
            marketOpen: new TimeOnly(0, 0),
            marketClose: new TimeOnly(0, 0),
            boothOpen: new TimeOnly(18, 0),
            boothClose: new TimeOnly(23, 0),
            lat: 10.77m,
            lng: 106.69m);

        var repository = new AssistantFoodQueryRepository(db);
        var result = await repository.GetEligibleFoodsAsync(new AssistantFoodQueryCriteria
        {
            UtcNow = ClosedHoursUtc
        });

        Assert.Contains(result.Foods, item => item.FoodItem.Id == food.Id);
        Assert.Equal(0, result.Pipeline.AfterOpenNow);
    }

    [Fact]
    public async Task GetCurrentByIdAsync_ReturnsFoodRegardlessOfHours()
    {
        await using var db = CreateContext();
        var food = await SeedFoodAsync(
            db,
            marketOpen: new TimeOnly(18, 0),
            marketClose: new TimeOnly(23, 0),
            boothOpen: new TimeOnly(18, 0),
            boothClose: new TimeOnly(23, 0),
            lat: 10.77m,
            lng: 106.69m);

        var repository = new AssistantFoodQueryRepository(db);
        var current = await repository.GetCurrentByIdAsync(food.Id, ClosedHoursUtc);

        Assert.NotNull(current);
        Assert.Equal(food.Id, current!.FoodItem.Id);
    }

    [Fact]
    public void AddToCart_ClosedMarketAndBooth_AllowsWhenEntitiesActive()
    {
        var food = BuildFood(
            marketOpen: new TimeOnly(18, 0),
            marketClose: new TimeOnly(23, 0),
            boothOpen: new TimeOnly(18, 0),
            boothClose: new TimeOnly(23, 0));

        var result = CustomerOrderability.EvaluateForCartAdd(food, ClosedHoursUtc);

        Assert.True(result.CanOrder);
    }

    [Fact]
    public void Checkout_ClosedMarketAndBooth_RejectsOrder()
    {
        var food = BuildFood(
            marketOpen: new TimeOnly(18, 0),
            marketClose: new TimeOnly(23, 0),
            boothOpen: new TimeOnly(18, 0),
            boothClose: new TimeOnly(23, 0));

        var result = CustomerOrderability.Evaluate(food, ClosedHoursUtc);

        Assert.False(result.CanOrder);
        Assert.Equal(CustomerOrderability.MarketClosed, result.ReasonCode);
    }

    [Fact]
    public async Task GetEligibleFoods_NoMarketFilter_ReturnsFoodsAcrossMarkets()
    {
        await using var db = CreateContext();
        var first = await SeedFoodAsync(db, marketName: "Chợ A", lat: 10.77m, lng: 106.69m);
        var second = await SeedFoodAsync(db, marketName: "Chợ B", lat: 10.80m, lng: 106.72m);

        var repository = new AssistantFoodQueryRepository(db);
        var result = await repository.GetEligibleFoodsAsync(new AssistantFoodQueryCriteria
        {
            UtcNow = ClosedHoursUtc
        });

        Assert.Equal(2, result.Foods.Count);
        Assert.Contains(result.Foods, item => item.FoodItem.Id == first.Id);
        Assert.Contains(result.Foods, item => item.FoodItem.Id == second.Id);
    }

    [Fact]
    public async Task GetEligibleFoods_ConversationMarketIdIgnored_UnlessExplicitRequest()
    {
        await using var db = CreateContext();
        var marketAFood = await SeedFoodAsync(db, marketName: "Chợ A", lat: 10.77m, lng: 106.69m);
        await SeedFoodAsync(db, marketName: "Chợ B", lat: 10.80m, lng: 106.72m);

        var repository = new AssistantFoodQueryRepository(db);
        var scoped = await repository.GetEligibleFoodsAsync(new AssistantFoodQueryCriteria
        {
            MarketId = marketAFood.Booth.NightMarketId,
            UtcNow = ClosedHoursUtc
        });

        Assert.Single(scoped.Foods);
        Assert.Equal(marketAFood.Id, scoped.Foods[0].FoodItem.Id);
    }

    [Fact]
    public void Score_WithGps_NearestRanksBeforeFartherEvenWhenLowerCompatibility()
    {
        var near = Eligible("Gần", 30_000m, distanceMeters: 800, semantic: 0.7);
        var far = Eligible("Xa", 30_000m, distanceMeters: 2000, semantic: 0.98);
        var semantic = new AssistantSemanticMatchResult
        {
            Scores = new Dictionary<Guid, AssistantSemanticScore>
            {
                [near.FoodItem.Id] = new() { FoodItemId = near.FoodItem.Id, SemanticCompatibility = 0.7 },
                [far.FoodItem.Id] = new() { FoodItemId = far.FoodItem.Id, SemanticCompatibility = 0.98 }
            }
        };
        var scorer = CreateScorer(minimum: 0);

        var ranked = scorer.Score(
            [far, near],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.FOOD_RECOMMENDATION },
            semantic,
            ClosedHoursUtc);

        Assert.Equal(near.FoodItem.Id, ranked[0].Eligible.FoodItem.Id);
        Assert.Equal(far.FoodItem.Id, ranked[1].Eligible.FoodItem.Id);
    }

    [Fact]
    public async Task GetEligibleFoods_AllergyHardConstraint_ExcludesFood()
    {
        await using var db = CreateContext();
        var allergenId = Guid.NewGuid();
        var allergen = new Allergen { Id = allergenId, Code = "CRUSTACEAN", Name = "Crustacean", IsActive = true };
        db.Allergens.Add(allergen);
        await db.SaveChangesAsync();

        var safe = await SeedFoodAsync(db, foodName: "Bánh tráng an toàn", lat: 10.77m, lng: 106.69m);
        var unsafeFood = await SeedFoodAsync(db, foodName: "Bánh tráng tôm", lat: 10.77m, lng: 106.69m);
        db.FoodItemAllergens.Add(new FoodItemAllergen
        {
            FoodItemId = unsafeFood.Id,
            AllergenId = allergenId,
            DeclarationType = AllergenDeclarationType.CONTAINS
        });
        await db.SaveChangesAsync();

        var repository = new AssistantFoodQueryRepository(db);
        var result = await repository.GetEligibleFoodsAsync(new AssistantFoodQueryCriteria
        {
            AllergenExclusionIds = [allergenId],
            UtcNow = ClosedHoursUtc
        });

        Assert.Single(result.Foods);
        Assert.Equal(safe.Id, result.Foods[0].FoodItem.Id);
    }

    [Fact]
    public async Task GetEligibleFoods_BudgetHardConstraint_ExcludesExpensiveFood()
    {
        await using var db = CreateContext();
        var cheap = await SeedFoodAsync(db, foodName: "Bánh tráng rẻ", price: 25_000m, lat: 10.77m, lng: 106.69m);
        await SeedFoodAsync(db, foodName: "Bánh tráng đắt", price: 120_000m, lat: 10.77m, lng: 106.69m);

        var repository = new AssistantFoodQueryRepository(db);
        var result = await repository.GetEligibleFoodsAsync(new AssistantFoodQueryCriteria
        {
            BudgetMax = 50_000m,
            UtcNow = ClosedHoursUtc
        });

        var item = Assert.Single(result.Foods);
        Assert.Equal(cheap.Id, item.FoodItem.Id);
    }

    [Fact]
    public async Task GetEligibleFoods_NoGps_DoesNotExcludeFarFoodWithoutExplicitMaxDistance()
    {
        await using var db = CreateContext();
        await SeedFoodAsync(db, marketName: "Chợ gần", lat: 10.770m, lng: 106.690m);
        var far = await SeedFoodAsync(db, marketName: "Chợ xa", lat: 10.900m, lng: 106.900m);

        var repository = new AssistantFoodQueryRepository(db);
        var result = await repository.GetEligibleFoodsAsync(new AssistantFoodQueryCriteria
        {
            Latitude = 10.770,
            Longitude = 106.690,
            UtcNow = ClosedHoursUtc
        });

        Assert.Equal(2, result.Foods.Count);
        Assert.Contains(result.Foods, item => item.FoodItem.Id == far.Id);
        Assert.All(result.Foods, item => Assert.NotNull(item.DistanceMeters));
    }

    [Fact]
    public async Task GetEligibleFoods_GenericBanhTrangQuery_HasEligibleFoodsWhenClosed()
    {
        await using var db = CreateContext();
        await SeedFoodAsync(
            db,
            foodName: "Bánh tráng trộn",
            marketOpen: new TimeOnly(18, 0),
            marketClose: new TimeOnly(23, 0),
            boothOpen: new TimeOnly(18, 0),
            boothClose: new TimeOnly(23, 0),
            lat: 10.77m,
            lng: 106.69m);

        var repository = new AssistantFoodQueryRepository(db);
        var result = await repository.GetEligibleFoodsAsync(new AssistantFoodQueryCriteria
        {
            UtcNow = ClosedHoursUtc
        });

        Assert.True(result.Pipeline.AfterHardConstraints >= 1);
        Assert.Equal(result.Pipeline.AfterHardConstraints, result.Foods.Count);
    }

    [Fact]
    public void Score_OpenNowDoesNotBoostCompatibility()
    {
        var closed = Eligible("Đóng cửa", 30_000m, semantic: 0.8);
        var open = Eligible("Mở cửa", 30_000m, semantic: 0.8);
        open.FoodItem.Booth.NightMarket.OpeningHours = new TimeOnly(0, 0);
        open.FoodItem.Booth.NightMarket.ClosingHours = new TimeOnly(0, 0);

        var semantic = new AssistantSemanticMatchResult
        {
            Scores = new Dictionary<Guid, AssistantSemanticScore>
            {
                [closed.FoodItem.Id] = new() { FoodItemId = closed.FoodItem.Id, SemanticCompatibility = 0.8 },
                [open.FoodItem.Id] = new() { FoodItemId = open.FoodItem.Id, SemanticCompatibility = 0.8 }
            }
        };
        var scorer = CreateScorer(minimum: 0);
        var ranked = scorer.Score(
            [closed, open],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.FOOD_RECOMMENDATION },
            semantic,
            ClosedHoursUtc);

        Assert.Equal(2, ranked.Count);
        Assert.Equal(ranked[0].FinalScore, ranked[1].FinalScore, 4);
    }

    private static AssistantCompatibilityScorer CreateScorer(double minimum)
        => new(Options.Create(new AssistantOptions
        {
            MinimumCompatibilityScore = minimum,
            SemanticWeight = 1,
            StructuredPreferenceWeight = 0,
            PriceWeight = 0,
            RatingWeight = 0,
            FeaturedWeight = 0,
            PromoWeight = 0
        }));

    private static AssistantEligibleFood Eligible(
        string name,
        decimal price,
        double semantic,
        double? distanceMeters = null)
    {
        var market = new NightMarket
        {
            Id = Guid.NewGuid(),
            Name = "Chợ",
            Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active,
            OpeningHours = new TimeOnly(18, 0),
            ClosingHours = new TimeOnly(23, 0),
            Latitude = 10.77m,
            Longitude = 106.69m
        };
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            BoothName = "Quầy",
            NightMarket = market,
            NightMarketId = market.Id,
            Status = BoothStatus.Active
        };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            Name = name,
            Booth = booth,
            BoothId = booth.Id,
            Category = new FoodCategory { Id = Guid.NewGuid(), Name = "Món", Code = "MAIN" },
            Ingredients = [],
            TasteProfiles = [],
            PreparationMethods = [],
            Courses = []
        };
        return new AssistantEligibleFood
        {
            FoodItem = food,
            EffectivePrice = price,
            DistanceMeters = distanceMeters
        };
    }

    private static async Task<FoodItem> SeedFoodAsync(
        SNMDbContext db,
        string marketName = "Chợ đêm",
        string foodName = "Bánh tráng",
        decimal price = 30_000m,
        TimeOnly? marketOpen = null,
        TimeOnly? marketClose = null,
        TimeOnly? boothOpen = null,
        TimeOnly? boothClose = null,
        decimal lat = 10.77m,
        decimal lng = 106.69m)
    {
        var now = ClosedHoursUtc;
        var market = new NightMarket
        {
            Id = Guid.NewGuid(),
            Name = marketName,
            Address = "Ho Chi Minh City",
            Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active,
            OpeningHours = marketOpen ?? new TimeOnly(0, 0),
            ClosingHours = marketClose ?? new TimeOnly(0, 0),
            Latitude = lat,
            Longitude = lng,
            CreatedAt = now,
            UpdatedAt = now
        };
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            NightMarketId = market.Id,
            BoothOwnerId = Guid.NewGuid(),
            BoothName = "Quầy",
            Status = BoothStatus.Active,
            OpenTime = boothOpen,
            CloseTime = boothClose,
            NightMarket = market,
            CreatedAt = now,
            UpdatedAt = now
        };
        var category = new FoodCategory
        {
            Id = Guid.NewGuid(),
            BoothId = booth.Id,
            Name = "Món",
            Code = "MAIN",
            Booth = booth,
            CreatedAt = now,
            UpdatedAt = now
        };
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            BoothId = booth.Id,
            CategoryId = category.Id,
            Name = foodName,
            Price = price,
            IsAvailable = true,
            Booth = booth,
            Category = category,
            Ingredients = [],
            TasteProfiles = [],
            PreparationMethods = [],
            Courses = [],
            Allergens = [],
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AddRange(market, booth, category, food);
        await db.SaveChangesAsync();
        return food;
    }

    private static FoodItem BuildFood(
        TimeOnly marketOpen,
        TimeOnly marketClose,
        TimeOnly boothOpen,
        TimeOnly boothClose)
    {
        var market = new NightMarket
        {
            Id = Guid.NewGuid(),
            Name = "Chợ",
            Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active,
            OpeningHours = marketOpen,
            ClosingHours = marketClose
        };
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            BoothName = "Quầy",
            NightMarket = market,
            NightMarketId = market.Id,
            Status = BoothStatus.Active,
            OpenTime = boothOpen,
            CloseTime = boothClose
        };
        return new FoodItem
        {
            Id = Guid.NewGuid(),
            Name = "Bánh tráng",
            IsAvailable = true,
            Booth = booth,
            BoothId = booth.Id,
            Category = new FoodCategory { Id = Guid.NewGuid(), Name = "Món", Code = "MAIN" }
        };
    }

    private static SNMDbContext CreateContext()
        => new(new DbContextOptionsBuilder<SNMDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
