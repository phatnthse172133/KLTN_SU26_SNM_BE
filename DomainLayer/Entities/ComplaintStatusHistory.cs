using System;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

// Audit trail for complaint status transitions.
public partial class ComplaintStatusHistory
{
    public Guid Id { get; set; }

    public Guid ComplaintId { get; set; }

    public ComplaintStatus? FromStatus { get; set; }

    public ComplaintStatus ToStatus { get; set; }

    public string? Note { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? ActorRole { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Complaint Complaint { get; set; } = null!;
}
