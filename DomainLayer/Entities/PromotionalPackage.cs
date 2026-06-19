using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Danh sách các gói quảng bá - gian hàng mua để được ưu tiên hiển thị (IsFeatured = true)
public partial class PromotionalPackage
{
    public Guid Id { get; set; }

    public string PackageName { get; set; } = null!;

    public decimal Price { get; set; }

    public int DurationDays { get; set; }

    public string? Description { get; set; }

    public PromotionalPackageStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BoothPromotionalPackage> BoothPromotionalPackages { get; set; } = new List<BoothPromotionalPackage>();
}
