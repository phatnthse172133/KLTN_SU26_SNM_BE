using DomainLayer.Entities;
using DomainLayer.Enums;

namespace DomainLayer.Common;

public sealed class AssistantFoodQueryCriteria
{
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public int? MaxDistanceMeters { get; init; }
    public decimal? BudgetMin { get; init; }
    public decimal? BudgetMax { get; init; }
    public IReadOnlyCollection<Guid> AllergenExclusionIds { get; init; } = [];
    public IReadOnlyCollection<Guid> AvoidedIngredientIds { get; init; } = [];
    public IReadOnlyCollection<Guid> DietaryRequirementIds { get; init; } = [];
    public IReadOnlyCollection<Guid> AvoidedTasteProfileIds { get; init; } = [];
    public FoodSpiceLevel? MaxSpiceLevel { get; init; }
    public bool TreatMayContainAsHard { get; init; } = true;
    public DateTime UtcNow { get; init; }
}

public sealed class AssistantEligibleFood
{
    public required FoodItem FoodItem { get; init; }
    public decimal EffectivePrice { get; init; }
    public double? DistanceMeters { get; init; }
    public bool HasActivePromotion { get; init; }
    public int SoldToday { get; init; }
    public int OrderCount { get; init; }
}

public sealed class AssistantFoodQueryPipelineDiagnostics
{
    public int AfterMarketActive { get; init; }
    public int AfterBoothActive { get; init; }
    public int AfterFoodVisible { get; init; }
    public int AfterHardConstraints { get; init; }
    public int AfterOpenNow { get; init; }
}

public sealed class AssistantFoodQueryResult
{
    public IReadOnlyList<AssistantEligibleFood> Foods { get; init; } = [];
    public AssistantFoodQueryPipelineDiagnostics Pipeline { get; init; } = new();
}
