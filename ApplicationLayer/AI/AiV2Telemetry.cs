using System.Diagnostics.Metrics;

namespace ApplicationLayer.AI.V2;

public static class AiV2Telemetry
{
    private static readonly Meter Meter = new("SmartNightMarket.AI.V2", "2.0.0");
    public static readonly Counter<long> RecommendationRequests = Meter.CreateCounter<long>("ai_v2.recommendation.requests");
    public static readonly Counter<long> ProviderSuccesses = Meter.CreateCounter<long>("ai_v2.provider.successes");
    public static readonly Counter<long> ProviderFailures = Meter.CreateCounter<long>("ai_v2.provider.failures");
    public static readonly Counter<long> Fallbacks = Meter.CreateCounter<long>("ai_v2.fallbacks");
    public static readonly Counter<long> InvalidParses = Meter.CreateCounter<long>("ai_v2.parse.invalid");
    public static readonly Counter<long> Candidates = Meter.CreateCounter<long>("ai_v2.candidates");
    public static readonly Counter<long> NoResults = Meter.CreateCounter<long>("ai_v2.no_results");
    public static readonly Counter<long> StrongMatches = Meter.CreateCounter<long>("ai_v2.strong_matches");
    public static readonly Counter<long> ExplanationFallbacks = Meter.CreateCounter<long>("ai_v2.explanation.fallbacks");
    public static readonly Counter<long> FeedbackActions = Meter.CreateCounter<long>("ai_v2.feedback.actions");
    public static readonly Histogram<double> RecommendationDurationMs = Meter.CreateHistogram<double>("ai_v2.recommendation.duration", "ms");
    public static readonly Histogram<double> ProviderDurationMs = Meter.CreateHistogram<double>("ai_v2.provider.duration", "ms");
}
