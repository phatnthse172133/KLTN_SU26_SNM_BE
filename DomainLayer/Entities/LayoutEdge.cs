using System;
using System.Collections.Generic;
using DomainLayer.Common;

namespace DomainLayer.Entities;


// Cạnh nối giữa 2 LayoutNode - thể hiện đường đi và khoảng cách, dùng cho thuật toán tìm đường ngắn nhất trong chợ
public partial class LayoutEdge : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid LayoutId { get; set; }

    public Guid FromNodeId { get; set; }

    public Guid ToNodeId { get; set; }

    public decimal Distance { get; set; }

    public bool IsBidirectional { get; set; }

    public bool IsAccessible { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual MarketLayout Layout { get; set; } = null!;

    public virtual LayoutNode FromNode { get; set; } = null!;

    public virtual LayoutNode ToNode { get; set; } = null!;
}
