using System;

namespace DomainLayer.Entities;

/// <summary>
/// Bảng giá theo thời điểm của gói dịch vụ
/// </summary>
public partial class PackagePrice
{
    public Guid Id { get; set; }

    public Guid PackageId { get; set; }

    public decimal Price { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Package Package { get; set; } = null!;
}
