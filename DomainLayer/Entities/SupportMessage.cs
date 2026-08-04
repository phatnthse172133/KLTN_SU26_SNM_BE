namespace DomainLayer.Entities;

public class SupportMessage
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderRole { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsInternalNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual SupportTicket Ticket { get; set; } = null!;
    public virtual User Sender { get; set; } = null!;
    public virtual ICollection<SupportAttachment> Attachments { get; set; } = new List<SupportAttachment>();
}
