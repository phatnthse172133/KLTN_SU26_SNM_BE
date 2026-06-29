namespace ApplicationLayer.DTOs.Responses;

public class NodeAvailabilityResponse
{
    public Guid NodeId { get; set; }
    public bool IsAvailable { get; set; }
    public Guid? BoothId { get; set; }
}

public class NightMarketMapResponse
{
    public MapNightMarketResponse NightMarket { get; set; } = new();
    public MapLayoutResponse Layout { get; set; } = new();
    public IReadOnlyCollection<ZoneResponse> Zones { get; set; } = [];
    public IReadOnlyCollection<LayoutNodeResponse> StartingPoints { get; set; } = [];
    public IReadOnlyCollection<MapBoothResponse> Booths { get; set; } = [];
}

public class MapNightMarketResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class MapLayoutResponse
{
    public Guid Id { get; set; }
    public string? ImageUrl { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class MapBoothResponse
{
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public Guid NodeId { get; set; }
    public Guid? ZoneId { get; set; }
    public string? SlotNumber { get; set; }
    public decimal XCoordinate { get; set; }
    public decimal YCoordinate { get; set; }
}

public class NearestNodeResponse
{
    public Guid FromNodeId { get; set; }
    public string? NodeName { get; set; }
    public decimal Distance { get; set; }
}

public class ShortestPathResponse
{
    public Guid LayoutId { get; set; }
    public RouteNodeSummaryResponse FromNode { get; set; } = new();
    public RouteDestinationResponse Destination { get; set; } = new();
    public decimal TotalDistance { get; set; }
    public int EstimatedWalkingMinutes { get; set; }
    public IReadOnlyCollection<RoutePathNodeResponse> Path { get; set; } = [];
}

public class RouteNodeSummaryResponse
{
    public Guid NodeId { get; set; }
    public string? NodeName { get; set; }
}

public class RouteDestinationResponse
{
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public Guid NodeId { get; set; }
}

public class RoutePathNodeResponse
{
    public int Sequence { get; set; }
    public Guid NodeId { get; set; }
    public decimal XCoordinate { get; set; }
    public decimal YCoordinate { get; set; }
}
