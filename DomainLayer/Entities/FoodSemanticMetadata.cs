using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Entities;

[NotMapped]
public abstract class SemanticCatalogEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class Ingredient : SemanticCatalogEntity
{
    public string NormalizedName { get; set; } = null!;
    public ICollection<FoodItemIngredient> FoodItems { get; set; } = new List<FoodItemIngredient>();
}

public sealed class Allergen : SemanticCatalogEntity
{
    public ICollection<FoodItemAllergen> FoodItems { get; set; } = new List<FoodItemAllergen>();
}

public sealed class DietaryAttribute : SemanticCatalogEntity
{
    public ICollection<FoodItemDietaryAttribute> FoodItems { get; set; } = new List<FoodItemDietaryAttribute>();
}

public sealed class PreparationMethod : SemanticCatalogEntity
{
    public ICollection<FoodItemPreparationMethod> FoodItems { get; set; } = new List<FoodItemPreparationMethod>();
}

public sealed class TasteProfile : SemanticCatalogEntity
{
    public ICollection<FoodItemTasteProfile> FoodItems { get; set; } = new List<FoodItemTasteProfile>();
}

public sealed class FoodSearchFacet : SemanticCatalogEntity
{
    public ICollection<FoodItemSearchFacet> FoodItems { get; set; } = new List<FoodItemSearchFacet>();
}

public sealed class FoodItemIngredient
{
    public Guid FoodItemId { get; set; }
    public Guid IngredientId { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsOptional { get; set; }
    public DateTime CreatedAt { get; set; }
    public FoodItem FoodItem { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}

public sealed class FoodItemAllergen
{
    public Guid FoodItemId { get; set; }
    public Guid AllergenId { get; set; }
    public AllergenDeclarationType DeclarationType { get; set; }
    public bool IsConfirmed { get; set; }
    public MetadataSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public FoodItem FoodItem { get; set; } = null!;
    public Allergen Allergen { get; set; } = null!;
}

public sealed class FoodItemDietaryAttribute
{
    public Guid FoodItemId { get; set; }
    public Guid DietaryAttributeId { get; set; }
    public DietarySuitabilityStatus SuitabilityStatus { get; set; }
    public bool IsConfirmed { get; set; }
    public MetadataSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public FoodItem FoodItem { get; set; } = null!;
    public DietaryAttribute DietaryAttribute { get; set; } = null!;
}

public sealed class FoodItemPreparationMethod
{
    public Guid FoodItemId { get; set; }
    public Guid PreparationMethodId { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public FoodItem FoodItem { get; set; } = null!;
    public PreparationMethod PreparationMethod { get; set; } = null!;
}

public sealed class FoodItemTasteProfile
{
    public Guid FoodItemId { get; set; }
    public Guid TasteProfileId { get; set; }
    public int? Intensity { get; set; }
    public DateTime CreatedAt { get; set; }
    public FoodItem FoodItem { get; set; } = null!;
    public TasteProfile TasteProfile { get; set; } = null!;
}

public sealed class FoodItemSearchFacet
{
    public Guid FoodItemId { get; set; }
    public Guid FoodSearchFacetId { get; set; }
    public DateTime CreatedAt { get; set; }
    public FoodItem FoodItem { get; set; } = null!;
    public FoodSearchFacet FoodSearchFacet { get; set; } = null!;
}

public sealed class FoodItemCourse
{
    public Guid FoodItemId { get; set; }
    public FoodCourse Course { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public FoodItem FoodItem { get; set; } = null!;
}

public sealed class FoodItemDiningPurpose
{
    public Guid FoodItemId { get; set; }
    public DiningPurpose Purpose { get; set; }
    public DateTime CreatedAt { get; set; }
    public FoodItem FoodItem { get; set; } = null!;
}
