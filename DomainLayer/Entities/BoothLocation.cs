using System;
using System.Collections.Generic;
using DomainLayer.Common;

namespace DomainLayer.Entities;


// Vị trí cụ thể (tọa độ) của 1 gian hàng trên 1 sơ đồ mặt bằng
public partial class BoothLocation : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public Guid LayoutId { get; set; }

    public Guid LayoutNodeId { get; set; }

    public Guid? ZoneId { get; set; }

    public string? SlotNumber { get; set; }

    public decimal Xcoordinate { get; set; }

    public decimal Ycoordinate { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual MarketLayout Layout { get; set; } = null!;

    public virtual LayoutNode LayoutNode { get; set; } = null!;

    public virtual Zone? Zone { get; set; }
}
