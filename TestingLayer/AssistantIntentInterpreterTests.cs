using System.Text.Json;
using ApplicationLayer.Services.Assistant;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Options;
using Moq;

namespace TestingLayer;

public sealed class AssistantIntentInterpreterTests
{
    [Fact]
    public async Task Interpret_UsesStructuredJson_NotKeywordMatching()
    {
        var message = "Tôi muốn món cay, không hải sản";
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {
                  "intent": "FOOD_RECOMMENDATION",
                  "budgetMin": null,
                  "budgetMax": 150000,
                  "partySize": 2,
                  "needsLocation": false,
                  "hardConstraints": {
                    "allergenCodes": ["CRUSTACEAN"],
                    "avoidedIngredientCodes": [],
                    "dietaryCodes": [],
                    "maxSpiceLevel": null,
                    "avoidedTasteCodes": []
                  },
                  "structuredPreferences": {
                    "preferredIngredientCodes": [],
                    "preferredTasteCodes": ["SPICY"],
                    "preferredPreparationCodes": [],
                    "preferredCourseCodes": []
                  },
                  "semanticPreferences": [],
                  "semanticAvoidances": [],
                  "diningContext": "ăn tối",
                  "assistantReply": null,
                  "foodIds": ["00000000-0000-0000-0000-000000000099"]
                }
                """);

        var interpreter = new AssistantIntentInterpreter(llm.Object, Options.Create(new OpenAiOptions { Enabled = true, ApiKey = "test" }));
        var result = await interpreter.InterpretAsync(message, [], Catalog(), new AssistantStageAContext(), CancellationToken.None);

        Assert.Equal(AssistantIntentKind.FOOD_RECOMMENDATION, result.Intent);
        Assert.Equal(150000m, result.BudgetMax);
        Assert.Equal(2, result.PartySize);
        Assert.Contains("CRUSTACEAN", result.HardConstraints.AllergenCodes);
        Assert.Contains("SPICY", result.StructuredPreferences.PreferredTasteCodes);
        Assert.DoesNotContain("CRUSTACEAN", result.SemanticAvoidances);
        llm.Verify(client => client.CompleteJsonAsync(
            It.Is<string>(prompt => prompt.Contains("STAGE A", StringComparison.Ordinal)),
            It.Is<string>(user => HasRawOriginalMessage(user, message)),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Interpret_ExplicitPartyAndBudget_OverrideLlmGuess()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""
                {
                  "intent": "MEAL_PLAN",
                  "partySize": 9,
                  "budgetMax": 10000,
                  "needsLocation": false,
                  "hardConstraints": {},
                  "structuredPreferences": {},
                  "semanticPreferences": [],
                  "semanticAvoidances": []
                }
                """);
        var interpreter = new AssistantIntentInterpreter(llm.Object, Options.Create(new OpenAiOptions { Enabled = true, ApiKey = "test" }));

        var result = await interpreter.InterpretAsync(
            "Ăn tối bình dân",
            [],
            Catalog(),
            new AssistantStageAContext { PartySize = 4, Budget = 300_000m },
            CancellationToken.None);

        Assert.Equal(4, result.PartySize);
        Assert.Equal(300_000m, result.BudgetMax);
    }

    [Fact]
    public async Task Interpret_InvalidJson_ThrowsProviderUnavailable()
    {
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not-json{{{");
        var interpreter = new AssistantIntentInterpreter(llm.Object, Options.Create(new OpenAiOptions { ApiKey = "test" }));

        var exception = await Assert.ThrowsAsync<ApplicationLayer.Exceptions.AppException>(() =>
            interpreter.InterpretAsync("hello", [], Catalog(), new AssistantStageAContext(), CancellationToken.None));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("ASSISTANT_PROVIDER_UNAVAILABLE", exception.ErrorCode);
    }

    [Fact]
    public void Sanitize_MovesUnmappedCodesToSemanticArrays()
    {
        var parsed = new ParsedAssistantIntent
        {
            Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
            HardConstraints = new AssistantHardConstraints { AvoidedIngredientCodes = ["SEAWEED"] },
            StructuredPreferences = new AssistantStructuredPreferences { PreferredTasteCodes = ["SMOKY"] },
            SemanticPreferences = ["nhẹ bụng"],
            SemanticAvoidances = []
        };

        var sanitized = AssistantIntentInterpreter.Sanitize(parsed, Catalog());

        Assert.Empty(sanitized.HardConstraints.AvoidedIngredientCodes);
        Assert.Empty(sanitized.StructuredPreferences.PreferredTasteCodes);
        Assert.Contains("SEAWEED", sanitized.SemanticAvoidances);
        Assert.Contains("SMOKY", sanitized.SemanticPreferences);
        Assert.Contains("nhẹ bụng", sanitized.SemanticPreferences);
    }

    [Fact]
    public void Sanitize_KeepsUserGoalAndDiningContext()
    {
        var parsed = new ParsedAssistantIntent
        {
            Intent = AssistantIntentKind.FOOD_RECOMMENDATION,
            DiningContext = "  nhóm bạn, tối nay  ",
            UserGoal = "  muốn thử món mới  ",
            AdditionalMeaning = "  tối nay rảnh  ",
            SemanticPreferences = ["ngon ngon"]
        };

        var sanitized = AssistantIntentInterpreter.Sanitize(parsed, Catalog());

        Assert.Equal("nhóm bạn, tối nay", sanitized.DiningContext);
        Assert.Equal("muốn thử món mới", sanitized.UserGoal);
        Assert.Contains("ngon ngon", sanitized.SemanticPreferences);
        Assert.Equal("tối nay rảnh", sanitized.AdditionalMeaning);
    }

    [Fact]
    public async Task Interpret_SendsRawMessageAndGpsContext_WithoutKeywordExtraction()
    {
        string? captured = null;
        var llm = new Mock<ILanguageModelClient>();
        llm.Setup(client => client.CompleteJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, int, CancellationToken>((_, user, _, _) => captured = user)
            .ReturnsAsync("""
                {
                  "intent": "FOOD_RECOMMENDATION",
                  "needsLocation": false,
                  "hardConstraints": {},
                  "structuredPreferences": {},
                  "semanticPreferences": ["ngon ngon"],
                  "semanticAvoidances": [],
                  "additionalMeaning": "hôm nay rảnh"
                }
                """);

        var message = "Tìm 1 món ngon ngon cho hôm nay.";
        var interpreter = new AssistantIntentInterpreter(llm.Object, Options.Create(new OpenAiOptions { Enabled = true, ApiKey = "test" }));
        var marketId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var result = await interpreter.InterpretAsync(
            message,
            [],
            Catalog(),
            new AssistantStageAContext
            {
                MarketId = marketId,
                Latitude = 10.77,
                Longitude = 106.69,
                MaxDistanceMeters = 2000
            },
            CancellationToken.None);

        Assert.NotNull(captured);
        using var payload = JsonDocument.Parse(captured);
        Assert.Equal(message, payload.RootElement.GetProperty("originalMessage").GetString());
        var explicitContext = payload.RootElement.GetProperty("explicitContext");
        Assert.True(explicitContext.GetProperty("locationProvided").GetBoolean());
        Assert.Equal(marketId, explicitContext.GetProperty("marketId").GetGuid());
        Assert.Equal(10.77, explicitContext.GetProperty("latitude").GetDouble());
        Assert.Equal(106.69, explicitContext.GetProperty("longitude").GetDouble());
        Assert.Equal(2000, explicitContext.GetProperty("maxDistanceMeters").GetInt32());
        Assert.DoesNotContain("keyword", captured, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("hôm nay rảnh", result.AdditionalMeaning);
        Assert.Contains("ngon ngon", result.SemanticPreferences);
    }

    private static bool HasRawOriginalMessage(string user, string message)
    {
        using var payload = JsonDocument.Parse(user);
        return payload.RootElement.GetProperty("originalMessage").GetString() == message
            && payload.RootElement.TryGetProperty("explicitContext", out _)
            && !payload.RootElement.TryGetProperty("keywords", out _);
    }

    private static FoodSemanticCatalogSet Catalog()
        => new(
            [new Ingredient { Id = Guid.NewGuid(), Code = "PORK", Name = "Pork", IsActive = true }],
            [new Allergen { Id = Guid.NewGuid(), Code = "CRUSTACEAN", Name = "Crustacean", IsActive = true }],
            [new DietaryAttribute { Id = Guid.NewGuid(), Code = "VEGETARIAN", Name = "Vegetarian", IsActive = true }],
            [new PreparationMethod { Id = Guid.NewGuid(), Code = "GRILLED", Name = "Grilled", IsActive = true }],
            [new TasteProfile { Id = Guid.NewGuid(), Code = "SPICY", Name = "Spicy", IsActive = true }]);
}
