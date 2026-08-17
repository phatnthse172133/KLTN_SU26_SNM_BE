using System.Reflection;

namespace ApplicationLayer.Services.Assistant;

public static class AssistantPromptCatalog
{
    public static string IntentSystem => Load("intent-system.txt");
    public static string IntentSchema => Load("intent-schema.json");
    public static string SemanticSystem => Load("semantic-system.txt");
    public static string SemanticSchema => Load("semantic-schema.json");
    public static string MealPlanSystem => Load("meal-plan-system.txt");
    public static string MealPlanSchema => Load("meal-plan-schema.json");

    private static string Load(string fileName)
    {
        var assembly = typeof(AssistantPromptCatalog).Assembly;
        var resource = $"ApplicationLayer.Services.Assistant.Prompts.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded prompt {resource}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
