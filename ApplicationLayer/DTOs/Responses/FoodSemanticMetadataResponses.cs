using DomainLayer.Enums;
using ApplicationLayer.DTOs;
using System.Text.Json.Serialization;

namespace ApplicationLayer.DTOs.Responses;

public sealed record SemanticCatalogResponse(Guid Id, string Code, string Name);
public sealed record FoodAllergenDeclarationResponse(Guid Id, string Code, string Name,
    [property: JsonConverter(typeof(StrictEnumJsonConverter<AllergenDeclarationType>))] AllergenDeclarationType DeclarationType,
    bool IsConfirmed, [property: JsonConverter(typeof(StrictEnumJsonConverter<MetadataSource>))] MetadataSource Source);
public sealed record FoodDietaryAttributeResponse(Guid Id, string Code, string Name,
    [property: JsonConverter(typeof(StrictEnumJsonConverter<DietarySuitabilityStatus>))] DietarySuitabilityStatus SuitabilityStatus,
    bool IsConfirmed, [property: JsonConverter(typeof(StrictEnumJsonConverter<MetadataSource>))] MetadataSource Source);

public sealed class FoodSemanticMetadataResponse
{
    [JsonConverter(typeof(NullableEnumJsonConverter<FoodCourse>))] public FoodCourse? PrimaryCourse { get; set; }
    [JsonConverter(typeof(EnumCollectionJsonConverter<FoodCourse>))] public IReadOnlyCollection<FoodCourse> SupportedCourses { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> Ingredients { get; set; } = [];
    public IReadOnlyCollection<FoodAllergenDeclarationResponse> AllergenDeclarations { get; set; } = [];
    public IReadOnlyCollection<FoodDietaryAttributeResponse> DietaryAttributes { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> PreparationMethods { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> TasteProfiles { get; set; } = [];
    [JsonConverter(typeof(StrictEnumJsonConverter<FoodSpiceLevel>))] public FoodSpiceLevel SpiceLevel { get; set; }
    [JsonConverter(typeof(NullableEnumJsonConverter<ServingTemperature>))] public ServingTemperature? ServingTemperature { get; set; }
    public int? EstimatedServingCount { get; set; }
    public string? ServingSizeDescription { get; set; }
    public bool? IsShareable { get; set; }
}

public sealed class FoodItemV2Response : FoodItemResponse
{
    public FoodSemanticMetadataResponse SemanticMetadata { get; set; } = new();
}

public sealed class FoodMetadataCatalogResponse
{
    public IReadOnlyCollection<SemanticCatalogResponse> Ingredients { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> Allergens { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> DietaryAttributes { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> PreparationMethods { get; set; } = [];
    public IReadOnlyCollection<SemanticCatalogResponse> TasteProfiles { get; set; } = [];
}
