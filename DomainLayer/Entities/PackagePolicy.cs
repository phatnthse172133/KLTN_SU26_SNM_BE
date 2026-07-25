using DomainLayer.Common;
using System;

namespace DomainLayer.Entities;

public class PackagePolicy : ISoftDelete
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PackageId { get; set; }
    public virtual Package Package { get; set; } = null!;

    public string Version { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string ContentJson { get; set; } = null!;
    public string? ContentMarkdown { get; set; }

    public DateTime EffectiveFrom { get; set; }
    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
}
