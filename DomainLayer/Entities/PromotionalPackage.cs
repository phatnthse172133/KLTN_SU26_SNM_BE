using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Danh sách các gói quảng bá - gian hàng mua để được ưu tiên hiển thị (IsFeatured = true)
/// </summary>
public partial class PromotionalPackage
{
    public Guid Id { get; set; }

    public string PackageName { get; set; } = null!;

    public decimal Price { get; set; }

    public int DurationDays { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BoothPromotionalPackage> BoothPromotionalPackages { get; set; } = new List<BoothPromotionalPackage>();
}
