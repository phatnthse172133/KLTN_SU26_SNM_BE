namespace ApplicationLayer.Services.Assistant;

public sealed class CompactAssistantFoodCandidate
{
    public Guid FoodItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string SpiceLevel { get; set; } = string.Empty;
    public string? ServingTemperature { get; set; }
    public int? EstimatedServingCount { get; set; }
    public bool? IsShareable { get; set; }
    public decimal EffectivePrice { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public bool HasActivePromotion { get; set; }
    public bool IsFeatured { get; set; }
    public double? SemanticCompatibility { get; set; }
    public IReadOnlyList<string> IngredientCodes { get; set; } = [];
    public IReadOnlyList<string> AllergenCodes { get; set; } = [];
    public IReadOnlyList<string> DietaryCodes { get; set; } = [];
    public IReadOnlyList<string> TasteCodes { get; set; } = [];
    public IReadOnlyList<string> PreparationCodes { get; set; } = [];
    public IReadOnlyList<string> CourseCodes { get; set; } = [];
}
