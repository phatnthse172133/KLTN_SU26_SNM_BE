namespace ApplicationLayer.Services.Assistant;

public sealed class LanguageModelJsonSchemaOptions
{
    public required string SchemaJson { get; init; }
    public string Name { get; init; } = "response";
    public bool Strict { get; init; } = true;
}
