using System;
using System.Collections.Generic;
using DomainLayer.Common;
using DomainLayer.Enums;

namespace DomainLayer.Entities;


// Món ăn của từng gian hàng
public partial class FoodItem : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public Guid CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    // Giá mặc định. Nếu có FoodPrice theo ngày hiện tại thì giá đó được ưu tiên (override)
    public decimal Price { get; set; }

    public string? ThumbnailUrl { get; set; }

    // false khi món hết nguyên liệu hoặc chủ quán tạm ẩn
    public bool IsAvailable { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Cached from visible FoodReviews (same pattern as Booth.AverageRating).
    public decimal AverageRating { get; set; }

    public int ReviewCount { get; set; }

    public FoodSpiceLevel SpiceLevel { get; set; }

    public ServingTemperature? ServingTemperature { get; set; }

    private int? _estimatedServingCount;
    public int? EstimatedServingCount
    {
        get => _estimatedServingCount;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Serving count must be positive when provided.");
            _estimatedServingCount = value;
        }
    }

    public string? ServingSizeDescription { get; set; }

    public bool? IsShareable { get; set; }

    public int SemanticProfileVersion { get; set; }

    public DateTime? SemanticProfileUpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual FoodCategory Category { get; set; } = null!;

    public virtual ICollection<FoodImage> FoodImages { get; set; } = new List<FoodImage>();

    public virtual ICollection<FoodItemTag> FoodItemTags { get; set; } = new List<FoodItemTag>();

    public virtual ICollection<FoodPrice> FoodPrices { get; set; } = new List<FoodPrice>();

    public virtual ICollection<FoodItemIngredient> Ingredients { get; set; } = new List<FoodItemIngredient>();

    public virtual ICollection<FoodItemAllergen> Allergens { get; set; } = new List<FoodItemAllergen>();

    public virtual ICollection<FoodItemDietaryAttribute> DietaryAttributes { get; set; } = new List<FoodItemDietaryAttribute>();

    public virtual ICollection<FoodItemPreparationMethod> PreparationMethods { get; set; } = new List<FoodItemPreparationMethod>();

    public virtual ICollection<FoodItemTasteProfile> TasteProfiles { get; set; } = new List<FoodItemTasteProfile>();

    public virtual ICollection<FoodItemSearchFacet> SearchFacets { get; set; } = new List<FoodItemSearchFacet>();

    public virtual ICollection<FoodItemCourse> Courses { get; set; } = new List<FoodItemCourse>();

    public virtual ICollection<FoodItemDiningPurpose> DiningPurposes { get; set; } = new List<FoodItemDiningPurpose>();

    public virtual FoodAiProfile? AiProfile { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<FoodReview> FoodReviews { get; set; } = new List<FoodReview>();

    public virtual ICollection<PromotionFoodItem> PromotionFoodItems { get; set; } = new List<PromotionFoodItem>();
}
