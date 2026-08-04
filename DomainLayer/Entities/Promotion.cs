using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

public partial class Promotion : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public string? PromotionCode { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DiscountType DiscountType { get; set; }

    public PromotionScope Scope { get; set; }

    public decimal DiscountValue { get; set; }

    public decimal? MinimumOrderAmount { get; set; }

    public decimal? MaximumDiscountAmount { get; set; }

    public int? TotalUsageLimit { get; set; }

    public int? UsageLimitPerCustomer { get; set; }

    public bool IsPublic { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public PromotionStatus Status { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();

    public virtual ICollection<PromotionFoodItem> PromotionFoodItems { get; set; } = new List<PromotionFoodItem>();

    public virtual ICollection<PromotionCategory> PromotionCategories { get; set; } = new List<PromotionCategory>();
}
