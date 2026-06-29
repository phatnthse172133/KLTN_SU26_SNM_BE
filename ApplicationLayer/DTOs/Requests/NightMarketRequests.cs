using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class NightMarketListRequest : PaginationReq
{
    [StringLength(200)]
    public string? Keyword { get; set; }

    public NightMarketStatus? Status { get; set; }

    [RegularExpression("(?i)^(name|status|createdAt|updatedAt)$", ErrorMessage = "SortBy must be name, status, createdAt, or updatedAt.")]
    public string SortBy { get; set; } = "createdAt";

    [RegularExpression("(?i)^(asc|desc)$", ErrorMessage = "SortDirection must be asc or desc.")]
    public string SortDirection { get; set; } = "desc";
}

public class CreateNightMarketRequest
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required, StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [Required, Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Required, Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [Range(1, int.MaxValue)]
    public int BoundaryWidthMeters { get; set; }

    [Range(1, int.MaxValue)]
    public int BoundaryHeightMeters { get; set; }

    public TimeOnly? OpeningHours { get; set; }

    public TimeOnly? ClosingHours { get; set; }

    [Url, StringLength(500)]
    public string? ThumbnailUrl { get; set; }

    public NightMarketStatus Status { get; set; } = NightMarketStatus.Draft;
}

public class UpdateNightMarketRequest : CreateNightMarketRequest { }

public class UpdateNightMarketGeographicLocationRequest
{
    [Required, StringLength(500)]
    public string Address { get; set; } = string.Empty;

    [Required, Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Required, Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [Range(1, int.MaxValue)]
    public int BoundaryWidthMeters { get; set; }

    [Range(1, int.MaxValue)]
    public int BoundaryHeightMeters { get; set; }
}
