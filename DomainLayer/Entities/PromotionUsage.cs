using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

public partial class PromotionUsage
{
    public Guid Id { get; set; }

    public Guid PromotionId { get; set; }

    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public decimal DiscountAmount { get; set; }

    public PromotionUsageStatus Status { get; set; }

    public DateTime AppliedAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual Promotion Promotion { get; set; } = null!;
}
