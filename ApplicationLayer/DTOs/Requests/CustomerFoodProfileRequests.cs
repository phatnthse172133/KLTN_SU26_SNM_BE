using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ApplicationLayer.DTOs;
using DomainLayer.Enums;

namespace ApplicationLayer.DTOs.Requests;

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
