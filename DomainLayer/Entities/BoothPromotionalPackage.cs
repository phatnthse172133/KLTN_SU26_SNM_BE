using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;


// Lịch sử mua/sử dụng gói quảng bá của gian hàng
public partial class BoothPromotionalPackage
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public Guid PromotionalPackageId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;

    public virtual PromotionalPackage PromotionalPackage { get; set; } = null!;
}
