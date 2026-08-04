using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

// ─── Night Market Moderation ───────────────────────────────────

public class AdminMarketModerationQueryRequest : PaginationReq
{
    [StringLength(200)]
    public string? Keyword { get; set; }

    public NightMarketStatus? LifecycleStatus { get; set; }

    public ModerationStatus? ModerationStatus { get; set; }

    public Guid? MarketOwnerId { get; set; }

    [RegularExpression("(?i)^(name|status|createdAt|updatedAt)$")]
    public string SortBy { get; set; } = "createdAt";

    [RegularExpression("(?i)^(asc|desc)$")]
    public string SortDirection { get; set; } = "desc";
}

// ─── Booth Moderation ──────────────────────────────────────────

public class AdminBoothModerationQueryRequest : PaginationReq
{
    [StringLength(200)]
    public string? Keyword { get; set; }

    public BoothStatus? Status { get; set; }

    public Guid? NightMarketId { get; set; }

    public Guid? BoothOwnerId { get; set; }

    [RegularExpression("(?i)^(boothName|status|createdAt|updatedAt)$")]
    public string SortBy { get; set; } = "createdAt";

    [RegularExpression("(?i)^(asc|desc)$")]
    public string SortDirection { get; set; } = "desc";
}

public class BoothModerationActionRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;

    public DateTime? ExpectedUpdatedAt { get; set; }

    public Guid? ComplaintId { get; set; }
}

// ─── Shared ────────────────────────────────────────────────────

public class ChangeModerationStatusRequest
{
    [Required]
    public ModerationStatus Status { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;

    public DateTime? ExpectedUpdatedAt { get; set; }

    public Guid? ComplaintId { get; set; }
}
