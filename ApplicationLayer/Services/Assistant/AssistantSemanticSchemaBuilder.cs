using System.Text.Json;

namespace ApplicationLayer.Services.Assistant;

public static class AssistantSemanticSchemaBuilder
{
    public static string Build(int candidateCount)
    {
        if (candidateCount < 1)
            throw new ArgumentOutOfRangeException(nameof(candidateCount));

        var schema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "scores" },
            properties = new
            {
                scores = new
                {
                    type = "array",
                    minItems = candidateCount,
                    maxItems = candidateCount,
                    description = $"Exactly {candidateCount} direct-suitability scores in candidate order; scores[i] applies to candidate[i]. Penalize missing central requested concept; incidental overlap only = low score.",
                    items = new
                    {
                        type = "number",
                        minimum = 0,
                        maximum = 1
                    }
                }
            }
        };

        return JsonSerializer.Serialize(schema);
    }
}
