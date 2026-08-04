using DomainLayer.Common;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities;

public class LayoutNavigationAnchor : ISoftDelete
{
    public Guid Id { get; set; }
    public Guid LayoutId { get; set; }
    public Guid LayoutNodeId { get; set; }
    public NavigationAnchorType AnchorType { get; set; }
    public string AnchorCode { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsCustomerAccessible { get; set; }
    public bool IsActive { get; set; }
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
    public string? PublicTokenHash { get; set; }
    public int TokenVersion { get; set; } = 1;
    public bool IsQrEnabled { get; set; }
    public DateTime? QrValidFrom { get; set; }
    public DateTime? QrValidUntil { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public MarketLayout Layout { get; set; } = null!;
    public LayoutNode LayoutNode { get; set; } = null!;
}
