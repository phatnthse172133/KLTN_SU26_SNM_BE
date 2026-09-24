using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

/// <summary>
/// A versioned composition of the physical layout sections for one night market.
/// MarketMap.Version and MarketLayout.Version describe different version scopes.
/// </summary>
public class MarketMap
{
    public const string LegacyDraftName = "Legacy Editable Draft Map";

    public Guid Id { get; set; }

    public Guid NightMarketId { get; set; }

    public string Name { get; set; } = null!;

    public int Version { get; set; }

    public MarketMapStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual NightMarket NightMarket { get; set; } = null!;

    public virtual ICollection<MarketLayout> MarketLayouts { get; set; } = new List<MarketLayout>();
}
