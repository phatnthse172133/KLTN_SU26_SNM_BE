namespace ApplicationLayer.Services.Assistant;

/// <summary>
/// Config-driven semantic relevance filter based on Stage B positional scores.
/// Applied after scoring all eligible candidates and before proximity-based ranking.
/// </summary>
public static class AssistantSemanticRelevanceGate
{
    public static bool IsRelevant(double semanticScore, AssistantOptions options)
        => semanticScore >= options.MinimumSemanticRelevanceScore;

    public static IReadOnlyList<AssistantScoredFood> Filter(
        IReadOnlyList<AssistantScoredFood> scored,
        AssistantOptions options)
        => scored.Where(item => IsRelevant(item.SemanticScore, options)).ToArray();
}
