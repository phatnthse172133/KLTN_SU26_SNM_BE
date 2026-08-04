using System;
using System.Collections.Generic;

namespace DomainLayer.Entities;


// Thư viện ảnh (gallery) của gian hàng
public partial class BoothImage
{
    public Guid Id { get; set; }

    public Guid BoothId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Booth Booth { get; set; } = null!;
}