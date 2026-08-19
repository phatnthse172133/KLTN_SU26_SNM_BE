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
            MinimumSemanticRelevanceScore = 0,
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
            MinimumSemanticRelevanceScore = 0,
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
            MinimumSemanticRelevanceScore = 0,
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

    [Fact]
    public void Score_SemanticallyRelated_OutranksNearbyUnrelated()
    {
        var related = Eligible("Bún bò Huế", 45_000m, reviews: 10, rating: 4.5m, distanceMeters: 800);
        var unrelated = Eligible("Trà sữa", 25_000m, reviews: 20, rating: 4.9m, distanceMeters: 50);
        var semantic = SemanticScores(
            (related.FoodItem.Id, 0.82),
            (unrelated.FoodItem.Id, 0.18));
        var ranked = CreateScorer(minimumSemantic: 0.55, minimumCompat: 0).Score(
            [unrelated, related],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.FOOD_RECOMMENDATION },
            semantic,
            DateTime.UtcNow);

        Assert.Single(ranked);
        Assert.Equal(related.FoodItem.Id, ranked[0].Eligible.FoodItem.Id);
    }

    [Fact]
    public void Score_NearbyUnrelated_DoesNotBecomeFalseMatch()
    {
        var unrelated = Eligible("Kem dừa", 20_000m, reviews: 15, rating: 4.8m, distanceMeters: 30);
        var semantic = SemanticScores((unrelated.FoodItem.Id, 0.22));
        var ranked = CreateScorer(minimumSemantic: 0.55, minimumCompat: 0).Score(
            [unrelated],
            new ParsedAssistantIntent
            {
                Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
                SemanticPreferences = ["đồ nướng cay"]
            },
            semantic,
            DateTime.UtcNow);

        Assert.Empty(ranked);
    }

    [Fact]
    public void Score_NoSemanticMatch_ReturnsEmptyNotFabricated()
    {
        var a = Eligible("Món A", 30_000m, reviews: 5, rating: 4m, distanceMeters: 100);
        var b = Eligible("Món B", 35_000m, reviews: 8, rating: 4.2m, distanceMeters: 200);
        var semantic = SemanticScores(
            (a.FoodItem.Id, 0.35),
            (b.FoodItem.Id, 0.28));
        var ranked = CreateScorer(minimumSemantic: 0.55, minimumCompat: 0).Score(
            [a, b],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.FOOD_RECOMMENDATION },
            semantic,
            DateTime.UtcNow);

        Assert.Empty(ranked);
    }

    [Fact]
    public void Score_RelevantPool_OrdersByDistanceThenCompatibilityThenRating()
    {
        var nearLow = Eligible("Gần", 40_000m, reviews: 4, rating: 4.0m, distanceMeters: 100);
        var farHigh = Eligible("Xa", 40_000m, reviews: 20, rating: 4.8m, distanceMeters: 900);
        var semantic = SemanticScores(
            (nearLow.FoodItem.Id, 0.70),
            (farHigh.FoodItem.Id, 0.85));
        var ranked = CreateScorer(minimumSemantic: 0.55, minimumCompat: 0).Score(
            [farHigh, nearLow],
            new ParsedAssistantIntent { Intent = AssistantIntentKind.FOOD_RECOMMENDATION },
            semantic,
            DateTime.UtcNow);

        Assert.Equal(2, ranked.Count);
        Assert.Equal(nearLow.FoodItem.Id, ranked[0].Eligible.FoodItem.Id);
        Assert.Equal(farHigh.FoodItem.Id, ranked[1].Eligible.FoodItem.Id);
        Assert.True(ranked[1].FinalScore > ranked[0].FinalScore);
    }

    [Fact]
    public void Score_Paraphrases_UseSameSemanticGatePolicy()
    {
        var food = Eligible("Bánh xèo", 30_000m, reviews: 6, rating: 4.3m);
        var semantic = SemanticScores((food.FoodItem.Id, 0.68));
        var scorer = CreateScorer(minimumSemantic: 0.55, minimumCompat: 0);
        var intent = new ParsedAssistantIntent
        {
            Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
            SemanticPreferences = ["giòn rụm"]
        };

        var rankedA = scorer.Score([food], intent, semantic, DateTime.UtcNow);
        var paraphrase = new ParsedAssistantIntent
        {
            Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
            SemanticPreferences = ["món giòn tan"]
        };
        var rankedB = scorer.Score([food], paraphrase, semantic, DateTime.UtcNow);

        Assert.Single(rankedA);
        Assert.Single(rankedB);
    }

    [Theory]
    [InlineData(0.54, false)]
    [InlineData(0.55, true)]
    [InlineData(0.72, true)]
    public void RelevanceGate_UsesConfiguredSemanticFloor(double semanticScore, bool expectedPass)
    {
        var options = new AssistantOptions { MinimumSemanticRelevanceScore = 0.55 };
        Assert.Equal(expectedPass, AssistantSemanticRelevanceGate.IsRelevant(semanticScore, options));
    }

    private static AssistantCompatibilityScorer CreateScorer(double minimumSemantic, double minimumCompat)
        => new(Options.Create(new AssistantOptions
        {
            MinimumSemanticRelevanceScore = minimumSemantic,
            MinimumCompatibilityScore = minimumCompat,
            SemanticWeight = 0.40,
            StructuredPreferenceWeight = 0.20,
            PriceWeight = 0.15,
            RatingWeight = 0.10,
            FeaturedWeight = 0.03,
            PromoWeight = 0.02
        }));

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

    private static AssistantCompatibilityScorer Create()
        => new(Options.Create(new AssistantOptions { MinimumCompatibilityScore = 0.1, MinimumSemanticRelevanceScore = 0 }));

    private static AssistantEligibleFood Eligible(
        string name,
        decimal price,
        int reviews,
        decimal rating,
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
            AverageRating = rating,
            ReviewCount = reviews,
            Ingredients = [],
            TasteProfiles = [],
            PreparationMethods = [],
            Courses = []
        };
        return new AssistantEligibleFood { FoodItem = food, EffectivePrice = price, DistanceMeters = distanceMeters };
    }
}
