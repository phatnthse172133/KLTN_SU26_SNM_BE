using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

// â”€â”€â”€ Night Market Moderation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

// â”€â”€â”€ Booth Moderation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

// â”€â”€â”€ Shared â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
