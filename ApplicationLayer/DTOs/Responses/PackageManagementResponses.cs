namespace ApplicationLayer.DTOs.Responses;

public class PackageResponse
{
    public Guid Id { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
