using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Responses;

// ─── Night Market Moderation ───────────────────────────────────

public class MarketModerationOverviewResponse
{
    public Guid Id { get; set; }
    public string? MarketOwnerName { get; set; }
    public string? MarketOwnerEmail { get; set; }
    public string MarketName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? OpeningHours { get; set; }
    public string? ClosingHours { get; set; }
    public string LifecycleStatus { get; set; } = string.Empty;
    public string ModerationStatus { get; set; } = string.Empty;
    public int TotalBooths { get; set; }
    public int ActiveBooths { get; set; }
    public int TotalComplaintCount { get; set; }
    public int SeriousComplaintCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MarketModerationDetailResponse : MarketModerationOverviewResponse
{
    public string? Description { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? OwnerPhone { get; set; }
    public List<ModerationComplaintSummaryResponse> RecentComplaints { get; set; } = new();
}

// ─── Booth Moderation ──────────────────────────────────────────

public class BoothModerationOverviewResponse
{
    public Guid Id { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public string? BoothOwnerName { get; set; }
    public string? BoothOwnerEmail { get; set; }
    public string? NightMarketName { get; set; }
    public string? ZoneName { get; set; }
    public string? SlotNumber { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? AverageRating { get; set; }
    public int ComplaintCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BoothModerationDetailResponse : BoothModerationOverviewResponse
{
    public string? Description { get; set; }
    public string? PhoneNumber { get; set; }
    public string? OpenTime { get; set; }
    public string? CloseTime { get; set; }
    public List<ModerationComplaintSummaryResponse> RecentComplaints { get; set; } = new();
    public List<BoothDocumentResponse> Documents { get; set; } = new();
}

// ─── Shared ────────────────────────────────────────────────────

public class ModerationComplaintSummaryResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ModerationActionResponse
{
    public Guid Id { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class ModerationActionHistoryResponse
{
    public Guid Id { get; set; }
    public Guid? BoothId { get; set; }
    public Guid? NightMarketId { get; set; }
    public Guid AdminId { get; set; }
    public string? AdminName { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Guid? ComplaintId { get; set; }
    public DateTime CreatedAt { get; set; }
}
