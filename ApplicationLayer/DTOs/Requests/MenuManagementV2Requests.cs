using System.ComponentModel.DataAnnotations;
using DomainLayer.Enums;
using System.Text.Json.Serialization;
using ApplicationLayer.DTOs;

namespace ApplicationLayer.DTOs.Requests;

public sealed class FoodAllergenDeclarationRequest
{
    public Guid AllergenId { get; set; }
    [JsonConverter(typeof(StrictEnumJsonConverter<AllergenDeclarationType>))] public AllergenDeclarationType DeclarationType { get; set; }
}

public class CreateFoodItemV2Request
{
    public Guid CategoryId { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Range(0.01, 999999999)] public decimal Price { get; set; }
    [Url, StringLength(500)] public string? ThumbnailUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }
    [JsonConverter(typeof(NullableEnumJsonConverter<FoodCourse>))] public FoodCourse? PrimaryCourse { get; set; }
    [JsonConverter(typeof(EnumCollectionJsonConverter<FoodCourse>))] public IReadOnlyCollection<FoodCourse> AdditionalCourses { get; set; } = [];
    public IReadOnlyCollection<Guid> IngredientIds { get; set; } = [];
    public IReadOnlyCollection<FoodAllergenDeclarationRequest> ConfirmedAllergenDeclarations { get; set; } = [];
    public IReadOnlyCollection<Guid> DietaryAttributeIds { get; set; } = [];
    public IReadOnlyCollection<Guid> PreparationMethodIds { get; set; } = [];
    public IReadOnlyCollection<Guid> TasteProfileIds { get; set; } = [];
    [JsonConverter(typeof(StrictEnumJsonConverter<FoodSpiceLevel>))] public FoodSpiceLevel SpiceLevel { get; set; } = FoodSpiceLevel.UNKNOWN;
    [JsonConverter(typeof(NullableEnumJsonConverter<ServingTemperature>))] public ServingTemperature? ServingTemperature { get; set; }
    [Range(1, 100)] public int EstimatedServingCount { get; set; }
    [StringLength(300)] public string? ServingSizeDescription { get; set; }
    public bool IsShareable { get; set; }
}

public sealed class UpdateFoodItemV2Request : CreateFoodItemV2Request { }
