namespace DomainLayer.Entities;

public class SupportStatusHistory
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid ActorId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual SupportTicket Ticket { get; set; } = null!;
    public virtual User Actor { get; set; } = null!;
}
