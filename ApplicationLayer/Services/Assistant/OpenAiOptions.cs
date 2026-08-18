namespace ApplicationLayer.Services.Assistant;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string Provider { get; set; } = "OpenAI";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxInputCharacters { get; set; } = 1000;
    public int MaxOutputTokens { get; set; } = 400;
    public int RetryCount { get; set; } = 1;
    public string ReasoningEffort { get; set; } = "low";
    public int MaxOutputTokensIntent { get; set; } = 900;
    public int MaxOutputTokensSemantic { get; set; } = 2500;
    public int MaxOutputTokensMealPlan { get; set; } = 2500;
    public string ApiKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
