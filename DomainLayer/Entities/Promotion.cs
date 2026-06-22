using System;
using System.Collections.Generic;
using DomainLayer.Enums;

namespace DomainLayer.Entities;

/// <summary>
/// Chương trình khuyến mãi / mã giảm giá (voucher) do gian hàng tạo
/// </summary>
public partial class Promotion
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    /// <summary>
    /// Mã khuyến mãi / mã voucher (ví dụ: GIAM20K, FREESHIP)
    /// </summary>
    public string? PromotionCode { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Percentage: giảm % | FixedAmount: giảm số tiền cố định
    /// </summary>
    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public PromotionStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual ICollection<PromotionUsage> PromotionUsages { get; set; } = new List<PromotionUsage>();
}
