using System;
using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class PackagePromotionRequest
{
    public Guid? Id { get; set; }

    [Range(0, 999999999)]
    public decimal Price { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}

public class CreatePackageRequest
{
    [Required, StringLength(100)]
    public string PackageName { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal Price { get; set; }

    [Range(1, 3650)]
    public int DurationDays { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public PackageStatus Status { get; set; } = PackageStatus.Active;

    [Required, StringLength(50)]
    public string? TemplateCode { get; set; }

    /// <summary>Optional: attach a time-bound promotion at creation time.</summary>
    public PackagePromotionRequest? Promotion { get; set; }
}

public class UpdatePackageRequest
{
    [Required, StringLength(100)]
    public string PackageName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 999999999)]
    public decimal Price { get; set; }

    [Range(1, 3650)]
    public int DurationDays { get; set; }

    public PackageStatus Status { get; set; } = PackageStatus.Active;

    [RegularExpression("(?i)^(Keep|Upsert|Remove)$", ErrorMessage = "PromotionAction must be Keep, Upsert, or Remove.")]
    public string PromotionAction { get; set; } = "Keep";

    /// <summary>Optional: attach or update a time-bound promotion.</summary>
    public PackagePromotionRequest? Promotion { get; set; }
}
