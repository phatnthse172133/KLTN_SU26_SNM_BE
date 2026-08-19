namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantTurnTiming
{
    public long TotalMs { get; init; }
    public long IntentMs { get; init; }
    public long CandidateQueryMs { get; init; }
    public long DistanceCalculationMs { get; init; }
    public long HardConstraintMs { get; init; }
    public long RankingMs { get; init; }
    public long PersistenceMs { get; init; }
    public long MealPlanMs { get; init; }
}
