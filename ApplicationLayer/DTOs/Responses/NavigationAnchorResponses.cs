namespace ApplicationLayer.DTOs.Responses;

public class NavigationEntranceCollectionResponse
{
    public Guid MarketId { get; set; }
    public Guid LayoutId { get; set; }
    public int LayoutVersion { get; set; }
    public int GraphRevision { get; set; }
    public IReadOnlyCollection<NavigationEntranceResponse> Entrances { get; set; } = [];
}

public class NavigationEntranceResponse
{
    public Guid AnchorId { get; set; }
    public Guid NodeId { get; set; }
    public string AnchorCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsEntrance { get; set; }
    public bool IsExit { get; set; }
    public bool IsAccessible { get; set; }
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
    public double? OutdoorDistanceMeters { get; set; }
    public decimal? IndoorDistanceMeters { get; set; }
    public int? IndoorEstimatedWalkingMinutes { get; set; }
}

public class NavigationAnchorAdminResponse : NavigationEntranceResponse
{
    public Guid LayoutId { get; set; }
    public string AnchorType { get; set; } = string.Empty;
    public bool IsCustomerAccessible { get; set; }
    public bool IsActive { get; set; }
}
