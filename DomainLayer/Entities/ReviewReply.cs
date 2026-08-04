using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;


// Phản hồi của chủ gian hàng đối với đánh giá - quan hệ 1-1 với Reviews
public partial class ReviewReply
{
    public Guid Id { get; set; }

    public Guid ReviewId { get; set; }

    public Guid BoothOwnerId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User BoothOwner { get; set; } = null!;

    public virtual Review Review { get; set; } = null!;
}