using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public sealed class CreateMarketMapDraftRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<Guid> LayoutIds { get; set; } = [];
}

public sealed class ArrangeMarketMapRequest
{
    [Required, MinLength(1)]
    public List<MarketMapLayoutArrangementRequest> Layouts { get; set; } = [];
}

public sealed class MarketMapLayoutArrangementRequest
{
    public Guid LayoutId { get; set; }

    [Range(0, double.MaxValue)]
    public double OffsetXMeters { get; set; }

    [Range(0, double.MaxValue)]
    public double OffsetYMeters { get; set; }

    [Range(0, 1000)]
    public int DisplayOrder { get; set; }
}

public sealed class SetMarketMapDefaultLayoutRequest
{
    public Guid LayoutId { get; set; }
}
