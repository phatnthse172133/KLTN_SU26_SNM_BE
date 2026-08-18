using System.Text.Json.Serialization;
using DomainLayer.Enums;

namespace ApplicationLayer.Services.Assistant;

public sealed class ParsedAssistantIntent
{
    [JsonConverter(typeof(LlmEnumConverter<AssistantIntentKind>))]
    public AssistantIntentKind Intent { get; set; }

    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public int? PartySize { get; set; }
    public bool NeedsLocation { get; set; }
    public AssistantHardConstraints HardConstraints { get; set; } = new();
    public AssistantStructuredPreferences StructuredPreferences { get; set; } = new();
    public IReadOnlyList<string> SemanticPreferences { get; set; } = [];
    public IReadOnlyList<string> SemanticAvoidances { get; set; } = [];
    public string? DiningContext { get; set; }
    public string? UserGoal { get; set; }
    public string? AdditionalMeaning { get; set; }
    public string? AssistantReply { get; set; }
}

public sealed class AssistantStageAContext
{
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public int? MaxDistanceMeters { get; init; }
    public int? PartySize { get; init; }
    public decimal? Budget { get; init; }
}

public sealed class AssistantHardConstraints
{
    public IReadOnlyList<string> AllergenCodes { get; set; } = [];
    public IReadOnlyList<string> AvoidedIngredientCodes { get; set; } = [];
    public IReadOnlyList<string> DietaryCodes { get; set; } = [];
    [JsonConverter(typeof(LlmNullableEnumConverter<FoodSpiceLevel>))]
    public FoodSpiceLevel? MaxSpiceLevel { get; set; }
    public IReadOnlyList<string> AvoidedTasteCodes { get; set; } = [];
}

public sealed class AssistantStructuredPreferences
{
    public IReadOnlyList<string> PreferredIngredientCodes { get; set; } = [];
    public IReadOnlyList<string> PreferredTasteCodes { get; set; } = [];
    public IReadOnlyList<string> PreferredPreparationCodes { get; set; } = [];
    public IReadOnlyList<string> PreferredCourseCodes { get; set; } = [];
}
