using System.Net;
using System.Text;
using System.Text.Json;
using ApplicationLayer.AI.V2.Configuration;
using ApplicationLayer.Services.Menus;
using DomainLayer.Entities;
using DomainLayer.Enums;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Cores.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace TestingLayer;

public sealed class FoodAiProfileEnrichmentTests
{
    [Fact]
    public async Task Success_marks_profile_READY_and_sets_ai_fields()
    {
        var food = PendingFood();
        var handler = Handler(Ok(ValidEnrichmentJson()));
        var enricher = Enricher(handler, food);

        await enricher.EnrichOneAsync(food.Id);

        Assert.Equal(1, handler.Calls);
        Assert.Equal(FoodAiProfileStatus.READY, food.AiProfile!.Status);
        Assert.Equal("gpt-4o-mini", food.AiProfile.GeneratedByModel);
        Assert.Equal(0.82m, food.AiProfile.Confidence);
        Assert.Contains("nướng", food.AiProfile.AiDescription!, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(food.AiProfile.StructuredProfileJson));
        Assert.Null(food.AiProfile.LastError);
        Assert.Contains("method_grilled", food.AiProfile.SearchText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Provider_failure_marks_FAILED_without_throwing()
    {
        var food = PendingFood();
        var handler = Handler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var enricher = Enricher(handler, food);

        var exception = await Record.ExceptionAsync(() => enricher.EnrichOneAsync(food.Id));

        Assert.Null(exception);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(FoodAiProfileStatus.FAILED, food.AiProfile!.Status);
        Assert.Equal("PROVIDER_TRANSIENT", food.AiProfile.LastError);
        Assert.NotNull(food.AiProfile.LastAttemptAt);
    }

    [Fact]
    public async Task Unchanged_READY_hash_skips_OpenAI_call()
    {
        var food = PendingFood();
        new FoodAiProfileGenerator().Rebuild(food, DateTime.UtcNow);
        food.AiProfile!.Status = FoodAiProfileStatus.READY;
        food.AiProfile.AiDescription = "existing";
        food.AiProfile.GeneratedByModel = "gpt-4o-mini";
        food.AiProfile.Confidence = 0.9m;
        var handler = Handler(Ok(ValidEnrichmentJson()));
        var enricher = Enricher(handler, food);

        await enricher.EnrichOneAsync(food.Id);

        Assert.Equal(0, handler.Calls);
        Assert.Equal(FoodAiProfileStatus.READY, food.AiProfile.Status);
        Assert.Equal("existing", food.AiProfile.AiDescription);
    }

    [Fact]
    public async Task Schema_accepts_response_with_unknown_safe_omission()
    {
        var food = PendingFood();
        var json = """
            {
              "categoryCode":"MAIN",
              "mainIngredientCodes":["ING_BEEF"],
              "tasteCodes":[],
              "cookingMethodCodes":[],
              "mealTypeCodes":["MAIN_COURSE"],
              "servingStyles":[],
              "dietaryFlags":[],
              "spicyLevel":"UNKNOWN",
              "aiDescription":null,
              "confidence":0.4,
              "unknowns":["cookingMethod","spice"]
            }
            """;
        var handler = Handler(Ok(json));
        var enricher = Enricher(handler, food);

        await enricher.EnrichOneAsync(food.Id);

        Assert.Equal(FoodAiProfileStatus.READY, food.AiProfile!.Status);
        Assert.Null(food.AiProfile.AiDescription);
        Assert.Equal(0.4m, food.AiProfile.Confidence);
        Assert.Contains("unknowns", food.AiProfile.StructuredProfileJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disabled_provider_leaves_PENDING_without_http()
    {
        var food = PendingFood();
        var handler = Handler(Ok(ValidEnrichmentJson()));
        var enricher = Enricher(handler, food, enabled: false);

        await enricher.EnrichOneAsync(food.Id);

        Assert.Equal(0, handler.Calls);
        Assert.Equal(FoodAiProfileStatus.PENDING, food.AiProfile!.Status);
    }

    private static OpenAiFoodAiProfileEnricher Enricher(RecordingHandler handler, FoodItem food, bool enabled = true)
    {
        var foods = new Mock<IFoodItemRepository>();
        foods.Setup(x => x.GetSemanticProfileBatchAsync(food.Id, 1, It.IsAny<CancellationToken>())).ReturnsAsync([food]);
        foods.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);

        var metadata = new Mock<IFoodSemanticMetadataRepository>();
        metadata.Setup(x => x.GetActiveCatalogsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new FoodSemanticCatalogSet(
            [Catalog<Ingredient>("ING_BEEF"), Catalog<Ingredient>("ING_CHICKEN")],
            [],
            [Catalog<DietaryAttribute>("DIET_VEGETARIAN")],
            [Catalog<PreparationMethod>("METHOD_GRILLED"), Catalog<PreparationMethod>("METHOD_FRIED")],
            [Catalog<TasteProfile>("TASTE_SWEET"), Catalog<TasteProfile>("TASTE_SPICY")]));

        var runtime = Options.Create(new AiProviderRuntimeOptions
        {
            Enabled = enabled,
            Provider = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-4o-mini",
            TimeoutSeconds = 5,
            IntentTemperature = 0
        });
        var client = new OpenAiV2Client(
            new HttpClient(handler),
            runtime,
            Options.Create(new OpenAiSecretOptions { ApiKey = "test-key" }),
            NullLogger<OpenAiV2Client>.Instance);

        return new OpenAiFoodAiProfileEnricher(
            client, foods.Object, metadata.Object, runtime, NullLogger<OpenAiFoodAiProfileEnricher>.Instance);
    }

    private static FoodItem PendingFood()
    {
        var food = new FoodItem
        {
            Id = Guid.NewGuid(),
            Name = "Bò nướng",
            Description = "Thịt bò nướng",
            Price = 75000,
            IsAvailable = true,
            Category = new FoodCategory { Id = Guid.NewGuid(), Code = "MAIN", Name = "Main" },
            Courses = [new() { Course = FoodCourse.MAIN_COURSE, IsPrimary = true }],
            Ingredients =
            [
                new()
                {
                    IngredientId = Guid.NewGuid(),
                    Ingredient = Catalog<Ingredient>("ING_BEEF"),
                    IsPrimary = true
                }
            ]
        };
        new FoodAiProfileGenerator().Rebuild(food, DateTime.UtcNow);
        return food;
    }

    private static T Catalog<T>(string code) where T : SemanticCatalogEntity, new()
        => new() { Id = Guid.NewGuid(), Code = code, Name = code, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private static RecordingHandler Handler(HttpResponseMessage response)
        => new((_, _) => Task.FromResult(Clone(response)));

    private static HttpResponseMessage Ok(string providerJson) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = providerJson } } } }),
            Encoding.UTF8,
            "application/json")
    };

    private static HttpResponseMessage Clone(HttpResponseMessage response) => new(response.StatusCode)
    {
        Content = response.Content is null
            ? null
            : new StringContent(response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), Encoding.UTF8, "application/json")
    };

    private static string ValidEnrichmentJson() => """
        {
          "categoryCode":"MAIN",
          "mainIngredientCodes":["ING_BEEF"],
          "tasteCodes":[],
          "cookingMethodCodes":["METHOD_GRILLED"],
          "mealTypeCodes":["MAIN_COURSE"],
          "servingStyles":["HOT"],
          "dietaryFlags":[],
          "spicyLevel":"NON_SPICY",
          "aiDescription":"Thịt bò nướng thơm",
          "confidence":0.82,
          "unknowns":[]
        }
        """;

    private sealed class RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return await callback(request, cancellationToken);
        }
    }
}
