using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Chương trình khuyến mãi/mã giảm giá do gian hàng tạo
/// </summary>
public partial class Promotion
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Percentage: giảm % | FixedAmount: giảm số tiền cố định
    /// </summary>
    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>
    /// NULL = không giới hạn số lần sử dụng
    /// </summary>
    public int? UsageLimit { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();
}
