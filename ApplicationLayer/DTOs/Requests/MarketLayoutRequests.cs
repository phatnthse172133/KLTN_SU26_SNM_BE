using System.ComponentModel.DataAnnotations;
using ApplicationLayer.Helppers;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public class MarketLayoutListRequest : PaginationReq
{
    [StringLength(200)]
    public string? Keyword { get; set; }

    public MarketLayoutStatus? Status { get; set; }

    [RegularExpression("(?i)^(name|version|status|createdAt|updatedAt)$")]
    public string SortBy { get; set; } = "createdAt";

    [RegularExpression("(?i)^(asc|desc)$")]
    public string SortDirection { get; set; } = "desc";
}

public class CreateMarketLayoutRequest
{
    [Required, StringLength(150)]
    public string LayoutName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Version { get; set; } = 1;
}

public class UpdateMarketLayoutRequest : CreateMarketLayoutRequest { }

public class UpdateMarketLayoutImageRequest
{
    [Required, Url, StringLength(500)]
    public string LayoutImageUrl { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Width { get; set; }

    [Range(1, int.MaxValue)]
    public int Height { get; set; }
}
