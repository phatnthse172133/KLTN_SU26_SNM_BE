using System.Net.Http.Json;
using System.Text.Json;
using ApplicationLayer.AI;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Cores.AI;

public class GeminiAIProviderService : IAIProviderService
{
    private readonly HttpClient _httpClient;
    private readonly AIProviderSettings _settings;

    public GeminiAIProviderService(HttpClient httpClient, IOptions<AIProviderSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<FoodIntentDto> ParseFoodIntentAsync(
        string? userQuery,
        IReadOnlyCollection<string> allowedTags,
        CancellationToken cancellationToken = default)
    {
        var localIntent = ParseLocally(userQuery, allowedTags);
        if (!_settings.EnableExternalProvider || string.IsNullOrWhiteSpace(_settings.ApiKey) || string.IsNullOrWhiteSpace(userQuery))
        {
            return localIntent;
        }

        try
        {
            var prompt =
                "Parse this Vietnamese/English food request into strict JSON.\n"
                + $"Allowed tag codes: {string.Join(", ", allowedTags)}.\n"
                + $"Request: {userQuery}\n"
                + "JSON shape: {\"matchedTagNames\":[],\"avoidTagNames\":[],\"budgetMax\":null,\"diningStyle\":null}\n"
                + "Return JSON only.";

            var endpoint = $"{_settings.BaseUrl.TrimEnd('/')}/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";
            var geminiRequest = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(endpoint, geminiRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return localIntent;
            }

            using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            var text = document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            var json = ExtractJson(text);
            var providerIntent = JsonSerializer.Deserialize<FoodIntentDto>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            return providerIntent ?? localIntent;
        }
        catch
        {
            return localIntent;
        }
    }

    public Task<string> GenerateExplanationAsync(
        ExplanationContextDto context,
        CancellationToken cancellationToken = default)
    {
        var tags = context.MatchedTags.Count == 0
            ? "your request"
            : string.Join(", ", context.MatchedTags);
        var budget = context.BudgetMax.HasValue ? $" within budget {context.BudgetMax.Value:N0}" : string.Empty;
        var price = context.Price.HasValue ? $" at {context.Price.Value:N0}" : string.Empty;
        var rating = context.Rating.HasValue ? $" with rating {context.Rating.Value:0.0}" : string.Empty;
        var market = string.IsNullOrWhiteSpace(context.NightMarketName) ? string.Empty : $" in {context.NightMarketName}";
        return Task.FromResult($"{context.SubjectName} matches {tags}{price}{budget}{rating}{market}.");
    }

    private static FoodIntentDto ParseLocally(string? query, IReadOnlyCollection<string> allowedTags)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new FoodIntentDto();
        }

        var normalized = query.ToLowerInvariant();
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var avoid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddIfAllowed(matched, allowedTags, normalized, "SPICY", ["cay", "spicy"]);
        AddIfAllowed(matched, allowedTags, normalized, "HOT", ["nong", "nóng", "hot"]);
        AddIfAllowed(matched, allowedTags, normalized, "COLD", ["lanh", "lạnh", "cold"]);
        AddIfAllowed(matched, allowedTags, normalized, "GRILLED", ["nuong", "nướng", "grill"]);
        AddIfAllowed(matched, allowedTags, normalized, "FRIED", ["chien", "chiên", "fried"]);
        AddIfAllowed(matched, allowedTags, normalized, "DRINK", ["nuoc", "nước", "drink", "tra", "trà"]);
        AddIfAllowed(matched, allowedTags, normalized, "FULLMEAL", ["an no", "ăn no", "full"]);
        AddIfAllowed(matched, allowedTags, normalized, "SNACK", ["an vat", "ăn vặt", "snack"]);
        AddIfAllowed(matched, allowedTags, normalized, "SEAFOOD", ["hai san", "hải sản", "seafood"]);

        if (normalized.Contains("khong an hai san") || normalized.Contains("không ăn hải sản") || normalized.Contains("no seafood"))
        {
            matched.Remove("SEAFOOD");
            AddAllowed(avoid, allowedTags, "SEAFOOD");
            AddAllowed(avoid, allowedTags, "NOSEAFOOD");
        }

        return new FoodIntentDto
        {
            MatchedTagNames = matched.ToList(),
            AvoidTagNames = avoid.ToList(),
            BudgetMax = TryParseBudget(normalized),
            DiningStyle = matched.Contains("FULLMEAL") ? "FullMeal" : null
        };
    }

    private static void AddIfAllowed(
        HashSet<string> target,
        IReadOnlyCollection<string> allowedTags,
        string query,
        string tag,
        IReadOnlyCollection<string> keywords)
    {
        if (keywords.Any(query.Contains))
        {
            AddAllowed(target, allowedTags, tag);
        }
    }

    private static void AddAllowed(HashSet<string> target, IReadOnlyCollection<string> allowedTags, string tag)
    {
        if (allowedTags.Any(allowed => allowed.Equals(tag, StringComparison.OrdinalIgnoreCase)))
        {
            target.Add(tag);
        }
    }

    private static decimal? TryParseBudget(string query)
    {
        var digits = new string(query.Where(char.IsDigit).ToArray());
        if (!decimal.TryParse(digits, out var value) || value <= 0)
        {
            return null;
        }

        if (query.Contains("k") && value < 1000)
        {
            value *= 1000;
        }

        return value;
    }

    private static string ExtractJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "{}";
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}
