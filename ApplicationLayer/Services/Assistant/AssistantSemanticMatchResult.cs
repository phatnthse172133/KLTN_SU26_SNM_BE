namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantSemanticMatchResult
{
    public IReadOnlyDictionary<Guid, AssistantSemanticScore> Scores { get; init; } =
        new Dictionary<Guid, AssistantSemanticScore>();

    public int BatchCount { get; init; }

    public IReadOnlyList<Guid> IdsSent { get; init; } = [];
}

public sealed class AssistantSemanticScore
{
    public Guid FoodItemId { get; init; }
    public double SemanticCompatibility { get; init; }
    public double Confidence { get; init; } = 1d;
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public IReadOnlyList<string> UnknownDataFacets { get; init; } = [];
}
