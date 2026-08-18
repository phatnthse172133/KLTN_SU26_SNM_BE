namespace ApplicationLayer.Services.Assistant;

public sealed class LanguageModelJsonCompletion
{
    public string Content { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string? FinishReason { get; init; }
    public int ConfiguredMaxOutputTokens { get; init; }
    public int? OutputTokenCount { get; init; }
    public int ResponseCharacterCount { get; init; }
    public string? RequestId { get; init; }

    public static LanguageModelJsonCompletion FromContent(string content, string? finishReason = null, int configuredMaxOutputTokens = 0)
        => new()
        {
            Content = content,
            FinishReason = finishReason,
            ConfiguredMaxOutputTokens = configuredMaxOutputTokens,
            ResponseCharacterCount = content.Length
        };

    public static implicit operator LanguageModelJsonCompletion(string content)
        => FromContent(content);
}
