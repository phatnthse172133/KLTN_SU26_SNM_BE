namespace ApplicationLayer.Services.Assistant;

public interface ILanguageModelClient
{
    Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        CancellationToken cancellationToken = default);
}
