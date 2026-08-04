using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Responses;

public class PackageResponse
{
    public Guid Id { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string? Code { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public PackageType Type { get; set; }
    public string? Description { get; set; }
    public string? Entitlements { get; set; }
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
