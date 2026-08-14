namespace ApplicationLayer.Configuration;

public class IndoorNavigationOptions
{
    public const string SectionName = "IndoorNavigation";
    public double WalkingSpeedMetersPerMinute { get; set; } = 80d;
    public decimal MinimumInstructionSegmentMeters { get; set; } = 1m;
    public decimal MaximumSnapDistanceMeters { get; set; } = 8m;
    public decimal MaximumSnapDistanceLayoutUnits { get; set; } = 20m;
    public double PedestrianClearanceMeters { get; set; } = 0.40d;
    public double MinimumCorridorMeters { get; set; } = 0.80d;
}
