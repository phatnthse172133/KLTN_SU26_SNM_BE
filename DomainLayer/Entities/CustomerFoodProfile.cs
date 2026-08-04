using DomainLayer.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace DomainLayer.Entities;

public sealed class CustomerFoodProfile
{
    public Guid CustomerId { get; set; }
    public FoodSpiceLevel? PreferredSpiceLevel { get; set; }
    public decimal? PreferredPriceMin { get; set; }
    public decimal? PreferredPriceMax { get; set; }
    public int? DefaultMaxDistanceMeters { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User Customer { get; set; } = null!;
    public ICollection<CustomerPreferredIngredient> PreferredIngredients { get; set; } = new List<CustomerPreferredIngredient>();
    public ICollection<CustomerAvoidedIngredient> AvoidedIngredients { get; set; } = new List<CustomerAvoidedIngredient>();
    public ICollection<CustomerDietaryRequirement> DietaryRequirements { get; set; } = new List<CustomerDietaryRequirement>();
    public ICollection<CustomerAllergenExclusion> AllergenExclusions { get; set; } = new List<CustomerAllergenExclusion>();
    public ICollection<CustomerPreferredPreparationMethod> PreferredPreparationMethods { get; set; } = new List<CustomerPreferredPreparationMethod>();
    public ICollection<CustomerPreferredTasteProfile> PreferredTasteProfiles { get; set; } = new List<CustomerPreferredTasteProfile>();
    public ICollection<CustomerAvoidedTasteProfile> AvoidedTasteProfiles { get; set; } = new List<CustomerAvoidedTasteProfile>();
    public ICollection<CustomerPreferredCourse> PreferredCourses { get; set; } = new List<CustomerPreferredCourse>();
    public ICollection<CustomerPreferredDiningPurpose> PreferredDiningPurposes { get; set; } = new List<CustomerPreferredDiningPurpose>();

    public bool IsIngredientAllowed(Guid ingredientId) => AvoidedIngredients.All(item => item.IngredientId != ingredientId);
    public bool HasTasteConflict(Guid tasteProfileId) =>
        AvoidedTasteProfiles.Any(item => item.TasteProfileId == tasteProfileId)
        && PreferredTasteProfiles.Any(item => item.TasteProfileId == tasteProfileId);
}

[NotMapped]
public abstract class CustomerIngredientPreference
{
    public Guid CustomerId { get; set; }
    public Guid IngredientId { get; set; }
    public DateTime CreatedAt { get; set; }
    public CustomerFoodProfile CustomerFoodProfile { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
public sealed class CustomerPreferredIngredient : CustomerIngredientPreference { }
public sealed class CustomerAvoidedIngredient : CustomerIngredientPreference { }

public sealed class CustomerDietaryRequirement
{
    public Guid CustomerId { get; set; }
    public Guid DietaryAttributeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public CustomerFoodProfile CustomerFoodProfile { get; set; } = null!;
    public DietaryAttribute DietaryAttribute { get; set; } = null!;
}

public sealed class CustomerAllergenExclusion
{
    public Guid CustomerId { get; set; }
    public Guid AllergenId { get; set; }
    public DateTime CreatedAt { get; set; }
    public CustomerFoodProfile CustomerFoodProfile { get; set; } = null!;
    public Allergen Allergen { get; set; } = null!;
}

public sealed class CustomerPreferredPreparationMethod
{
    public Guid CustomerId { get; set; }
    public Guid PreparationMethodId { get; set; }
    public DateTime CreatedAt { get; set; }
    public CustomerFoodProfile CustomerFoodProfile { get; set; } = null!;
    public PreparationMethod PreparationMethod { get; set; } = null!;
}

[NotMapped]
public abstract class CustomerTastePreference
{
    public Guid CustomerId { get; set; }
    public Guid TasteProfileId { get; set; }
    public DateTime CreatedAt { get; set; }
    public CustomerFoodProfile CustomerFoodProfile { get; set; } = null!;
    public TasteProfile TasteProfile { get; set; } = null!;
}
public sealed class CustomerPreferredTasteProfile : CustomerTastePreference { }
public sealed class CustomerAvoidedTasteProfile : CustomerTastePreference { }

public sealed class CustomerPreferredCourse
{
    public Guid CustomerId { get; set; }
    public FoodCourse Course { get; set; }
    public DateTime CreatedAt { get; set; }
    public CustomerFoodProfile CustomerFoodProfile { get; set; } = null!;
}

public sealed class CustomerPreferredDiningPurpose
{
    public Guid CustomerId { get; set; }
    public DiningPurpose Purpose { get; set; }
    public DateTime CreatedAt { get; set; }
    public CustomerFoodProfile CustomerFoodProfile { get; set; } = null!;
}
