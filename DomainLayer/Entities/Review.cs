using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;

/// <summary>
/// Đánh giá của khách hàng cho gian hàng, gắn liền với 1 đơn hàng đã hoàn tất
/// </summary>
public partial class Review
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid OrderId { get; set; }

    public short Rating { get; set; }

    public string? Content { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>
    /// false: Admin ẩn review nhưng vẫn giữ dữ liệu để tính rating
    /// </summary>
    public bool IsVisible { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual User Customer { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual ReviewReply? ReviewReply { get; set; }
}
