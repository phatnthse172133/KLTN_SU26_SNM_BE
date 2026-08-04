using System;
using System.Collections.Generic;
using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Thông tin các chợ đêm - đơn vị quản lý cấp cao nhất, chứa nhiều Booth
public partial class NightMarket : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid? MarketOwnerId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string Address { get; set; } = null!;

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public TimeOnly? OpeningHours { get; set; }

    public TimeOnly? ClosingHours { get; set; }

    // Số lượng gian hàng - giá trị cache, đồng bộ qua trigger hoặc job định kỳ
    public int TotalBooth { get; set; }

    public int? BoundaryWidthMeters { get; set; }

    public int? BoundaryHeightMeters { get; set; }

    public string? ThumbnailUrl { get; set; }

    public NightMarketStatus Status { get; set; }

    public ModerationStatus ModerationStatus { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public string? DeletionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Booth> Booths { get; set; } = new List<Booth>();

    public virtual ICollection<NightMarketImage> NightMarketImages { get; set; } = new List<NightMarketImage>();

    public virtual ICollection<AIRecommendationLog> AIRecommendationLogs { get; set; } = new List<AIRecommendationLog>();

    public virtual ICollection<AiMealPlan> AiMealPlans { get; set; } = new List<AiMealPlan>();

    public virtual ICollection<MarketLayout> MarketLayouts { get; set; } = new List<MarketLayout>();

    public virtual ICollection<Zone> Zones { get; set; } = new List<Zone>();

    public virtual ICollection<BoothRegistration> BoothRegistrations { get; set; } = new List<BoothRegistration>();

    public virtual User? MarketOwner { get; set; }
}
