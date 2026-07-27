namespace DomainLayer.Common;

public sealed record CustomerRecommendationContext(
    IReadOnlyDictionary<Guid, int> TagQuantities,
    IReadOnlyDictionary<Guid, int> CategoryQuantities,
    IReadOnlyDictionary<Guid, int> BoothQuantities,
    IReadOnlySet<Guid> RecentFoodIds,
    IReadOnlySet<Guid> PositiveBoothIds,
    IReadOnlySet<Guid> NegativeBoothIds,
    decimal? TypicalUnitPrice)
{
    public static CustomerRecommendationContext Empty { get; } = new(
        new Dictionary<Guid, int>(),
        new Dictionary<Guid, int>(),
        new Dictionary<Guid, int>(),
        new HashSet<Guid>(),
        new HashSet<Guid>(),
        new HashSet<Guid>(),
        null);

    public bool HasHistory => TagQuantities.Count > 0 || CategoryQuantities.Count > 0 || BoothQuantities.Count > 0;
}
