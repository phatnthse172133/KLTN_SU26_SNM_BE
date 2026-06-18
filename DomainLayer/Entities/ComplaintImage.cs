using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Ảnh minh chứng đính kèm theo khiếu nại
/// </summary>
public partial class ComplaintImage
{
    public Guid Id { get; set; }

    public Guid ComplaintId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Complaint Complaint { get; set; } = null!;
}
