namespace ApplicationLayer.AI.V2.Configuration;

public sealed class AiProviderRuntimeOptions
{
    public const string SectionName = "OpenAI";
    public const string LegacySectionName = "AIProviderV2";

    public string Provider { get; set; } = "OpenAI";
    public bool Enabled { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ReasoningEffort { get; set; } = "low";
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxInputCharacters { get; set; } = 1000;
    public int MaxOutputTokens { get; set; } = 400;
    public decimal IntentTemperature { get; set; }
    public decimal ExplanationTemperature { get; set; } = 0.1m;
    public int RetryCount { get; set; } = 1;
}

public sealed class RecommendationV2Options
{
    public const string SectionName = "RecommendationV2";
    public int CandidateLimit { get; set; } = 300;
    public int MaximumPageSize { get; set; } = 50;
    public int MaximumQueryCharacters { get; set; } = 1000;
    public decimal MaximumSupportedPrice { get; set; } = 100_000_000m;
    public int MaximumDistanceMeters { get; set; } = 100_000;
    public int SessionLifetimeMinutes { get; set; } = 30;
    public int FeedbackWindowMinutes { get; set; } = 30;
    public int RetentionDays { get; set; } = 30;
    public int CleanupBatchSize { get; set; } = 100;
    /// <summary>Capstone default 0: one OpenAI call for intent only; reasons use deterministic builder.</summary>
    public int MaximumExplanationCalls { get; set; } = 0;
    public decimal StrongMatchThreshold { get; set; } = 75m;
    public decimal NearMatchThreshold { get; set; } = 60m;
    public decimal DiversityScoreWindow { get; set; } = 4m;
    public int MaximumSameBoothInTopResults { get; set; } = 2;
    public int MaximumSameCategoryInTopResults { get; set; } = 3;
}

public sealed class MealPlanV2Options
{
    public const string SectionName = "MealPlanV2";
    public int MaximumRequestCharacters { get; set; } = 1000;
    public int MaximumPartySize { get; set; } = 30;
    public decimal MaximumBudget { get; set; } = 100_000_000m;
    public int MaximumDistanceMeters { get; set; } = 100_000;
    public int CandidateLimit { get; set; } = 300;
    public int MaximumCandidatesPerMarket { get; set; } = 50;
    public int MaximumCandidatesPerCourse { get; set; } = 12;
    public int MaximumPlans { get; set; } = 3;
    public decimal MaximumPlanOverlap { get; set; } = .80m;
    public int SessionLifetimeMinutes { get; set; } = 120;
    public int ReadRetentionDays { get; set; } = 30;
    public int CleanupBatchSize { get; set; } = 100;
    public int MaximumAlternativesPageSize { get; set; } = 30;
}
