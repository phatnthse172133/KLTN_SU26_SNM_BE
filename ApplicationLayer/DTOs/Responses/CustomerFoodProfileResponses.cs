using System.Text.Json.Serialization;
using ApplicationLayer.DTOs;
using DomainLayer.Enums;

namespace ApplicationLayer.DTOs.Responses;

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
