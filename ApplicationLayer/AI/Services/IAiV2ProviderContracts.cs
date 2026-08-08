using ApplicationLayer.AI.V2.Models;

namespace ApplicationLayer.AI.V2.Services;

public interface IAiIntentExtractor
{
    Task<FoodRecommendationIntentExtractionResult> ExtractFoodRecommendationIntentAsync(FoodRecommendationIntentRequest request, CancellationToken cancellationToken);
    Task<MealPlanIntentExtractionResult> ExtractMealPlanIntentAsync(MealPlanIntentRequest request, CancellationToken cancellationToken);
}

public interface IAiExplanationGenerator
{
    Task<AiGeneratedTextResult> GenerateFoodRecommendationReasonAsync(FoodRecommendationExplanationContext context, CancellationToken cancellationToken);
}

public interface IFoodRecommendationFallbackParser
{
    FoodRecommendationIntentExtractionResult Parse(FoodRecommendationIntentRequest request);
    MealPlanIntentExtractionResult Parse(MealPlanIntentRequest request);
}

public interface IFoodRecommendationIntentNormalizer
{
    FoodRecommendationIntentNormalizationResult Normalize(FoodRecommendationIntent rawIntent, FoodRecommendationNormalizationContext context);
}

public interface IDeterministicRecommendationReasonBuilder
{
    string Build(FoodRecommendationExplanationContext context);
}
