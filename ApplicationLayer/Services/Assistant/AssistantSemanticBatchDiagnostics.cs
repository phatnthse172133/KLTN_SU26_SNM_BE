namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantSemanticBatchDiagnostics
{
    public int BatchIndex { get; init; }
    public int BatchSize { get; init; }
    public long DurationMs { get; init; }
    public int RetryCount { get; init; }
    public string? FinishReason { get; init; }
    public int IdsSent { get; init; }
    public int IdsReturned { get; init; }
}
