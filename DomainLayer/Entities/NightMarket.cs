using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Thông tin các chợ đêm - đơn vị quản lý cấp cao nhất, chứa nhiều Booth
public partial class NightMarket
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string Address { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public TimeOnly? OpeningHours { get; set; }

    public TimeOnly? ClosingHours { get; set; }


    // Số lượng gian hàng - giá trị cache, đồng bộ qua trigger hoặc job định kỳ
    public int TotalBooth { get; set; }

    public int? MapWidth { get; set; }

    public int? MapHeight { get; set; }

    public string? ThumbnailUrl { get; set; }

    public NightMarketStatus Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Booth> Booths { get; set; } = new List<Booth>();

    public virtual ICollection<MarketLayout> MarketLayouts { get; set; } = new List<MarketLayout>();
}
