namespace ApplicationLayer.AI;

public class AIProviderSettings
{
    public const string SectionName = "AIProvider";

    /// <summary>V1 is local-only; external LLM access is configured under the OpenAI / V2 path.</summary>
    public string Provider { get; set; } = "Local";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool EnableExternalProvider { get; set; }
    public int CandidateLimit { get; set; } = 200;
    public int MaxPreferenceTags { get; set; } = 20;
    public int MaxQueryLength { get; set; } = 500;
    public decimal MaxBudget { get; set; } = 100_000_000m;
    public int TimeoutSeconds { get; set; } = 8;
    public int MaxOutputTokens { get; set; } = 256;
    public int HistoryOrderLimit { get; set; } = 30;
    public int HistoryReviewLimit { get; set; } = 30;
}
