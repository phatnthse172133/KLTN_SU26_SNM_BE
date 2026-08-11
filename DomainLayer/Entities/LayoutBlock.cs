using System;
using DomainLayer.Common;

namespace DomainLayer.Entities;

public class LayoutBlock : ISoftDelete
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    // Type of block: e.g., "BoothRow", "Facility", "Entrance", "Walkway", "Exit"
    public string Type { get; set; } = null!;
    
    // Name or Label of the block
    public string Name { get; set; } = null!;
    // Position and Dimensions
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public Guid? ZoneId { get; set; }
    
    // JSON configuration for rows/columns/gap/path-width
    public string? ConfigJson { get; set; }
    
    public int DisplayOrder { get; set; }
    
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual MarketLayout Layout { get; set; } = null!;
    public virtual Zone? Zone { get; set; }
}
