using ApplicationLayer.Services.Assistant;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.Extensions.Options;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public sealed class AssistantCompatibilityScorerTests
{
    [Fact]
    public void Score_UnknownCalories_DoNotInventNutrients()
    {
        var food = Eligible("Bún bò", 40_000m, reviews: 4, rating: 4.5m);
        var semantic = new AssistantSemanticMatchResult
        {
            Scores = new Dictionary<Guid, AssistantSemanticScore>
            {
                [food.FoodItem.Id] = new()
                {
                    FoodItemId = food.FoodItem.Id,
                    SemanticCompatibility = 0.8,
                    Reasons = ["MISSING_DB_FIELD"],
                    UnknownDataFacets = ["calories"]
                }
            }
        };

        var ranked = Create().Score(
            [food],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.FOOD_RECOMMENDATION },
            semantic,
            DateTime.UtcNow);

        var item = Assert.Single(ranked);
        Assert.Contains("calories", item.UnknownDataFacets);
        Assert.Contains(item.Reasons, reason => reason.Contains("MISSING_DB_FIELD", StringComparison.Ordinal));
        Assert.DoesNotContain(item.Reasons, reason => reason.Contains("kcal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Score_ZeroReviews_RatingComponentIsZero()
    {
        var scorer = new AssistantCompatibilityScorer(Options.Create(new AssistantOptions
        {
            MinimumCompatibilityScore = 0,
            SemanticWeight = 0,
            StructuredPreferenceWeight = 0,
            PriceWeight = 0,
            RatingWeight = 1,
            DistanceWeight = 0,
            FeaturedWeight = 0,
            OpenNowWeight = 0,
            PromoWeight = 0
        }));
        var food = Eligible("Chả giò", 25_000m, reviews: 0, rating: 5m);
        var ranked = scorer.Score(
            [food],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.FOOD_RECOMMENDATION },
            new AssistantSemanticMatchResult(),
            DateTime.UtcNow);

        Assert.Equal(0d, Assert.Single(ranked).RatingScore);
        Assert.Equal(0d, ranked[0].FinalScore);
    }

    [Fact]
    public void Score_CombinesConfiguredWeights_NotSemanticAlone()
    {
        var pork = new Ingredient { Id = Guid.NewGuid(), Code = "PORK", Name = "Pork" };
        var food = Eligible("Thịt nướng", 40_000m, reviews: 0, rating: 0);
        food.FoodItem.Ingredients = new List<FoodItemIngredient>
        {
            new() { Ingredient = pork, IngredientId = pork.Id, FoodItem = food.FoodItem }
        };
        var eligible = new AssistantEligibleFood
        {
            FoodItem = food.FoodItem,
            EffectivePrice = food.EffectivePrice
        };
        var scorer = new AssistantCompatibilityScorer(Options.Create(new AssistantOptions
        {
            MinimumCompatibilityScore = 0,
            SemanticWeight = 0.40,
            StructuredPreferenceWeight = 0.20,
            PriceWeight = 0,
            RatingWeight = 0,
            DistanceWeight = 0,
            FeaturedWeight = 0,
            OpenNowWeight = 0,
            PromoWeight = 0
        }));
        var semantic = new AssistantSemanticMatchResult
        {
            Scores = new Dictionary<Guid, AssistantSemanticScore>
            {
                [food.FoodItem.Id] = new()
                {
                    FoodItemId = food.FoodItem.Id,
                    SemanticCompatibility = 0.5
                }
            }
        };

        var ranked = scorer.Score(
            [eligible],
            new ParsedAssistantIntent
            {
                Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
                StructuredPreferences = new AssistantStructuredPreferences { PreferredIngredientCodes = ["PORK"] }
            },
            semantic,
            DateTime.UtcNow);

        var item = Assert.Single(ranked);
        Assert.Equal(0.5d, item.SemanticScore);
        Assert.Equal(1d, item.StructuredScore);
        Assert.Equal(0.666667d, item.FinalScore, 4);
        Assert.NotEqual(item.SemanticScore, item.FinalScore);
    }

    [Fact]
    public void Score_BelowMinimumThreshold_IsDropped()
    {
        var scorer = new AssistantCompatibilityScorer(Options.Create(new AssistantOptions
        {
            MinimumCompatibilityScore = 0.9,
            SemanticWeight = 1,
            StructuredPreferenceWeight = 0,
            PriceWeight = 0,
            RatingWeight = 0,
            DistanceWeight = 0,
            FeaturedWeight = 0,
            OpenNowWeight = 0,
            PromoWeight = 0
        }));
        var food = Eligible("Chè", 15_000m, reviews: 0, rating: 0);
        var ranked = scorer.Score(
            [food],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.FOOD_RECOMMENDATION },
            new AssistantSemanticMatchResult(),
            DateTime.UtcNow);

        Assert.Empty(ranked);
    }

    private static AssistantCompatibilityScorer Create()
        => new(Options.Create(new AssistantOptions { MinimumCompatibilityScore = 0.1 }));

    private static AssistantEligibleFood Eligible(string name, decimal price, int reviews, decimal rating)
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
            AverageRating = rating,
            ReviewCount = reviews,
            Ingredients = [],
            TasteProfiles = [],
            PreparationMethods = [],
            Courses = []
        };
        return new AssistantEligibleFood { FoodItem = food, EffectivePrice = price };
    }
}
