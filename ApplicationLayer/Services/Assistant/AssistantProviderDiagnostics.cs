namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantProviderDiagnostics
{
    public string Stage { get; init; } = string.Empty;
    public string? FinishReason { get; init; }
    public string ParseFailureCategory { get; init; } = AssistantJsonParseClassifier.Other;
    public int ConfiguredMaxOutputTokens { get; init; }
    public int? OutputTokenCount { get; init; }
    public int ResponseCharacterCount { get; init; }
    public string Model { get; init; } = string.Empty;
    public string? RequestId { get; init; }
    public string? JsonPrefix { get; init; }

    public static AssistantProviderDiagnostics From(
        string stage,
        LanguageModelJsonCompletion? completion,
        string parseFailureCategory)
    {
        var content = completion?.Content ?? string.Empty;
        return new AssistantProviderDiagnostics
        {
            Stage = stage,
            FinishReason = completion?.FinishReason,
            ParseFailureCategory = parseFailureCategory,
            ConfiguredMaxOutputTokens = completion?.ConfiguredMaxOutputTokens ?? 0,
            OutputTokenCount = completion?.OutputTokenCount,
            ResponseCharacterCount = completion?.ResponseCharacterCount ?? content.Length,
            Model = completion?.Model ?? string.Empty,
            RequestId = completion?.RequestId,
            JsonPrefix = SafePrefix(content)
        };
    }

    internal static string? SafePrefix(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;
        var trimmed = content.TrimStart();
        if (trimmed.Length == 0)
            return null;
        if (trimmed[0] is not '{' and not '[')
            return trimmed[0] == '`' ? "`" : "non_json";
        var take = Math.Min(80, trimmed.Length);
        return trimmed[..take];
    }
}
