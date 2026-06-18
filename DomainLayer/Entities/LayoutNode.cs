using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Các điểm/nút (node) trên sơ đồ mặt bằng - là đỉnh của đồ thị dùng cho tìm đường nội bộ chợ
/// </summary>
public partial class LayoutNode
{
    public Guid Id { get; set; }

    public Guid LayoutId { get; set; }

    public string? NodeName { get; set; }

    public decimal Xcoordinate { get; set; }

    public decimal Ycoordinate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual MarketLayout Layout { get; set; } = null!;
}
