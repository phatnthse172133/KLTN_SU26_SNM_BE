using System;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

/// <summary>
/// Lá»‹ch sá»­ hÃ nh Ä‘á»™ng moderation cho Booth hoáº·c Night Market.
/// Má»™t báº£ng chung cho cáº£ hai domain, phÃ¢n biá»‡t qua BoothId / NightMarketId.
/// </summary>
public class ModerationActionHistory
{
    public Guid Id { get; set; }

    public Guid? BoothId { get; set; }

    public Guid? NightMarketId { get; set; }

    public Guid AdminId { get; set; }

    public string? AdminName { get; set; }

    /// <summary>
    /// Tráº¡ng thÃ¡i trÆ°á»›c khi thay Ä‘á»•i (Active / Suspended).
    /// </summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>
    /// Tráº¡ng thÃ¡i sau khi thay Ä‘á»•i (Active / Suspended).
    /// </summary>
    public string NewStatus { get; set; } = string.Empty;

    /// <summary>
    /// LÃ½ do admin Ä‘Æ°a ra khi suspend/restore.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Nguá»“n hÃ nh Ä‘á»™ng: DirectAdmin hoáº·c Complaint.
    /// </summary>
    public ModerationActionSource Source { get; set; }

    public Guid? ComplaintId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Booth? Booth { get; set; }

    public virtual NightMarket? NightMarket { get; set; }
}
