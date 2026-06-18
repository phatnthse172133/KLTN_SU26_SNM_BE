using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Cạnh nối giữa 2 LayoutNode - thể hiện đường đi và khoảng cách, dùng cho thuật toán tìm đường ngắn nhất trong chợ
/// </summary>
public partial class LayoutEdge
{
    public Guid Id { get; set; }

    public Guid FromNodeId { get; set; }

    public Guid ToNodeId { get; set; }

    public decimal Distance { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
