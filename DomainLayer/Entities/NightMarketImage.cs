using System;
using System.Collections.Generic;
using DomainLayer.Common;

namespace DomainLayer.Entities;


// Thư viện ảnh (gallery) của chợ đêm
public partial class NightMarketImage : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid NightMarketId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public bool IsCover { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual NightMarket NightMarket { get; set; } = null!;
}
