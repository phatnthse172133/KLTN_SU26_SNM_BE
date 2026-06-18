using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Vị trí cụ thể (tọa độ) của 1 gian hàng trên 1 sơ đồ mặt bằng
/// </summary>
public partial class BoothLocation
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public Guid LayoutId { get; set; }

    public decimal Xcoordinate { get; set; }

    public decimal Ycoordinate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual MarketLayout Layout { get; set; } = null!;
}
