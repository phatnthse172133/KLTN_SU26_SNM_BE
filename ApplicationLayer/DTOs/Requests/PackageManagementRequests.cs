using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class CreatePackageRequest
{
    [Required, StringLength(100)]
    public string PackageName { get; set; } = string.Empty;

    [Range(0.01, 999999999)]
    public decimal Price { get; set; }

    [Range(1, 3650)]
    public int DurationDays { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public PackageStatus Status { get; set; } = PackageStatus.Active;
}

public class UpdatePackageRequest : CreatePackageRequest { }
