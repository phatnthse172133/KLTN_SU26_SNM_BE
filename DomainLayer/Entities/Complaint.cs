using System;
using System.Collections.Generic;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;


// Khiếu nại của khách hàng về đơn hàng/gian hàng
public partial class Complaint
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Guid BoothId { get; set; }

    public Guid OrderId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string? AdminResponse { get; set; }

    public ComplaintStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual ICollection<ComplaintImage> ComplaintImages { get; set; } = new List<ComplaintImage>();

    public virtual User Customer { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
