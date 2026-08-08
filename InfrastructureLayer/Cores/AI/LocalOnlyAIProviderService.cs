using System.Globalization;
using System.Text.RegularExpressions;
using ApplicationLayer.AI;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;

namespace InfrastructureLayer.Cores.AI;

/// <summary>
/// V1 AI provider stub that never calls external HTTP. Always uses local deterministic parsing.
/// External LLM access for recommendations lives on the V2 OpenAI path only.
/// </summary>
public sealed class LocalOnlyAIProviderService : IAIProviderService
{
    public Task<FoodIntentDto> ParseFoodIntentAsync(
        string? userQuery,
        IReadOnlyCollection<string> allowedTags,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ParseLocally(userQuery, allowedTags));
    }

    public Task<string> GenerateExplanationAsync(
        ExplanationContextDto context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

        if (normalized.Contains("khong cay") || normalized.Contains("không cay")
            || normalized.Contains("not spicy") || normalized.Contains("no spicy"))
        {
            matched.Remove("SPICY");
            AddAllowed(avoid, allowedTags, "SPICY");
        }

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
        var matches = Regex.Matches(
            query,
            @"(?<!\d)(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>k|ngh[iị]n|ng[aà]n|tri[eệ]u|tr|m)?\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        decimal? largest = null;
        foreach (Match match in matches)
        {
            var raw = match.Groups["amount"].Value.Replace(',', '.');
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value <= 0)
                continue;

            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            if (unit is "k" or "nghìn" or "nghịn" or "ngàn") value *= 1_000;
            if (unit is "triệu" or "tr" or "m") value *= 1_000_000;
            largest = !largest.HasValue || value > largest.Value ? value : largest;
        }

        return largest;
    }
}
