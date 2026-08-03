namespace DomainLayer.Entities;

public class SupportAttachment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid? MessageId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual SupportTicket Ticket { get; set; } = null!;
    public virtual SupportMessage? Message { get; set; }
}
