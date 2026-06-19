using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;


// Sơ đồ mặt bằng của một chợ đêm - dùng làm nền để đặt các điểm (LayoutNodes) và gian hàng (BoothLocations)
public partial class MarketLayout
{
    public Guid Id { get; set; }

    public Guid NightMarketId { get; set; }

    public string? LayoutImageUrl { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BoothLocation> BoothLocations { get; set; } = new List<BoothLocation>();

    public virtual ICollection<LayoutNode> LayoutNodes { get; set; } = new List<LayoutNode>();

    public virtual NightMarket NightMarket { get; set; } = null!;
}
