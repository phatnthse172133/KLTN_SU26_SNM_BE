using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;


// Lịch sử sử dụng mã khuyến mãi - kiểm tra UsageLimit và chống dùng trùng
public partial class PromotionUsage
{
    public Guid Id { get; set; }

    public Guid PromotionId { get; set; }

    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User Customer { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual Promotion Promotion { get; set; } = null!;
}