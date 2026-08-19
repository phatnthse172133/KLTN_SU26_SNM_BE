namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantSemanticMatchResult
{
    public IReadOnlyDictionary<Guid, AssistantSemanticScore> Scores { get; init; } =
        new Dictionary<Guid, AssistantSemanticScore>();

    public int BatchCount { get; init; }

    public int LogicalBatchCount { get; init; }

    public int ProviderCallCount { get; init; }

    public IReadOnlyList<Guid> IdsSent { get; init; } = [];

    public long SemanticTotalMs { get; init; }

    public int SemanticRetryCount { get; init; }

    public IReadOnlyList<AssistantSemanticBatchDiagnostics> BatchDiagnostics { get; init; } = [];
}

public sealed class AssistantSemanticScore
{
    public Guid FoodItemId { get; init; }
    public double SemanticCompatibility { get; init; }
    public double Confidence { get; init; } = 1d;
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public IReadOnlyList<string> UnknownDataFacets { get; init; } = [];
}
