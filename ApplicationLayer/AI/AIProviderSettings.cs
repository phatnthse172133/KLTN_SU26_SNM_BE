namespace ApplicationLayer.AI;

public class AIProviderSettings
{
    public const string SectionName = "AIProvider";

    public string Provider { get; set; } = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-flash";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
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
