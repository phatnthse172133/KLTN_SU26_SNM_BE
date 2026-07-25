using System;

using DomainLayer.Common;

namespace DomainLayer.Entities;


// Báº£ng giÃ¡ theo thá»i Ä‘iá»ƒm cá»§a gÃ³i dá»‹ch vá»¥
public partial class PackagePrice : ISoftDelete
{
    public Guid Id { get; set; }

    public Guid PackageId { get; set; }

    public decimal Price { get; set; }

    public int DurationDays { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Package Package { get; set; } = null!;
}
