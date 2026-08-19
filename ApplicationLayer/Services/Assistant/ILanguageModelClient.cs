namespace ApplicationLayer.Services.Assistant;

public interface ILanguageModelClient
{
    Task<LanguageModelJsonCompletion> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        CancellationToken cancellationToken,
        LanguageModelJsonSchemaOptions? schemaOptions);
}
