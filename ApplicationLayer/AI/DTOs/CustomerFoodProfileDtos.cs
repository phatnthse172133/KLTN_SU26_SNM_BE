using System.ComponentModel.DataAnnotations;
using ApplicationLayer.DTOs.Responses;
using DomainLayer.Enums;
using ApplicationLayer.DTOs;
using System.Text.Json.Serialization;

namespace ApplicationLayer.AI.DTOs;

public sealed class UpdateCustomerFoodProfileRequest
{
    public IReadOnlyCollection<Guid> PreferredIngredientIds { get; set; } = [];
    public IReadOnlyCollection<Guid> AvoidedIngredientIds { get; set; } = [];
    public IReadOnlyCollection<Guid> DietaryRequirementIds { get; set; } = [];
    public IReadOnlyCollection<Guid> AllergenExclusionIds { get; set; } = [];
    public IReadOnlyCollection<Guid> PreferredPreparationMethodIds { get; set; } = [];
    public IReadOnlyCollection<Guid> PreferredTasteProfileIds { get; set; } = [];
    public IReadOnlyCollection<Guid> AvoidedTasteProfileIds { get; set; } = [];
    [JsonConverter(typeof(EnumCollectionJsonConverter<FoodCourse>))] public IReadOnlyCollection<FoodCourse> PreferredCourses { get; set; } = [];
    [JsonConverter(typeof(NullableEnumJsonConverter<FoodSpiceLevel>))] public FoodSpiceLevel? PreferredSpiceLevel { get; set; }
    [Range(typeof(decimal), "0", "999999999")] public decimal? PreferredPriceMin { get; set; }
    [Range(typeof(decimal), "0", "999999999")] public decimal? PreferredPriceMax { get; set; }
    [Range(1, 100000)] public int? DefaultMaxDistanceMeters { get; set; }
}

public sealed class CustomerFoodProfileResponse
{
    public IReadOnlyCollection<SemanticCatalogResponse> PreferredIngredients { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> AvoidedIngredients { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> DietaryRequirements { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> AllergenExclusions { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> PreferredPreparationMethods { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> PreferredTasteProfiles { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> AvoidedTasteProfiles { get; set; } = [];
    [JsonConverter(typeof(EnumCollectionJsonConverter<FoodCourse>))] public IReadOnlyCollection<FoodCourse> PreferredCourses { get; set; } = [];
    [JsonConverter(typeof(NullableEnumJsonConverter<FoodSpiceLevel>))] public FoodSpiceLevel? PreferredSpiceLevel { get; set; }
    public decimal? PreferredPriceMin { get; set; }
    public decimal? PreferredPriceMax { get; set; }
    public int? DefaultMaxDistanceMeters { get; set; }
}
