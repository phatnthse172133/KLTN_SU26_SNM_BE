namespace DomainLayer.Entities;

public class SupportTicket
{
    public Guid Id { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public Guid RequesterId { get; set; }
    public string RequesterRole { get; set; } = string.Empty;
    public Guid? BoothId { get; set; }
    public Guid? NightMarketId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string Priority { get; set; } = "Normal";
    public string? PageUrl { get; set; }
    public Guid? AssignedAdminId { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual User Requester { get; set; } = null!;
    public virtual User? AssignedAdmin { get; set; }
    public virtual ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
    public virtual ICollection<SupportAttachment> Attachments { get; set; } = new List<SupportAttachment>();
    public virtual ICollection<SupportStatusHistory> StatusHistory { get; set; } = new List<SupportStatusHistory>();
}
