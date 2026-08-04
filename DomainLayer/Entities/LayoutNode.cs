using System;
using System.Collections.Generic;
using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Các điểm/nút (node) trên sơ đồ mặt bằng - là đỉnh của đồ thị dùng cho tìm đường nội bộ chợ
public partial class LayoutNode : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid LayoutId { get; set; }

    public Guid? ZoneId { get; set; }

    public string? NodeName { get; set; }

    public LayoutNodeType NodeType { get; set; }

    public decimal Xcoordinate { get; set; }

    public decimal Ycoordinate { get; set; }

    public bool IsAccessible { get; set; }

    public bool IsStartingPoint { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual MarketLayout Layout { get; set; } = null!;

    public virtual Zone? Zone { get; set; }

    public virtual ICollection<LayoutEdge> OutgoingEdges { get; set; } = new List<LayoutEdge>();

    public virtual ICollection<LayoutEdge> IncomingEdges { get; set; } = new List<LayoutEdge>();

    public virtual ICollection<BoothLocation> BoothLocations { get; set; } = new List<BoothLocation>();

    public virtual ICollection<LayoutNavigationAnchor> NavigationAnchors { get; set; } = new List<LayoutNavigationAnchor>();
}
