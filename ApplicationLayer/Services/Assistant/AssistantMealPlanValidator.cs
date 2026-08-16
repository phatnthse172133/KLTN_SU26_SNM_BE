using DomainLayer.Entities;
using DomainLayer.Enums;
using Microsoft.Extensions.Options;

namespace ApplicationLayer.Services.Assistant;

public sealed class AssistantMealPlanValidator(IOptions<AssistantOptions> options)
{
    public static readonly FoodCourse[] StandardCourses =
    [
        FoodCourse.APPETIZER,
        FoodCourse.MAIN_COURSE,
        FoodCourse.SIDE_DISH,
        FoodCourse.SHARED_DISH,
        FoodCourse.DRINK,
        FoodCourse.DESSERT
    ];

    private static readonly FoodCourse[] DropOrder =
    [
        FoodCourse.EXTRA,
        FoodCourse.DESSERT,
        FoodCourse.DRINK,
        FoodCourse.SIDE_DISH,
        FoodCourse.SOUP,
        FoodCourse.APPETIZER,
        FoodCourse.SHARED_DISH,
        FoodCourse.MAIN_COURSE
    ];

    private readonly AssistantOptions _options = options.Value;

    public IReadOnlyList<AssistantMealPlanDraft> Validate(
        IReadOnlyList<AssistantMealPlanProposal> proposals,
        IReadOnlyList<AssistantScoredFood> ranked,
        ParsedAssistantIntent intent)
    {
        if (ranked.Count == 0)
            return [];

        var byId = ranked.ToDictionary(item => item.Eligible.FoodItem.Id);
        var partySize = Math.Max(1, intent.PartySize ?? 1);
        var budget = intent.BudgetMax;
        var maxOptions = Math.Max(1, _options.MaxMealPlanOptions);
        var maxItems = Math.Max(1, _options.MaxMealPlanItemsPerPlan);
        var accepted = new List<AssistantMealPlanDraft>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var proposal in proposals)
        {
            if (accepted.Count >= maxOptions)
                break;
            var draft = Materialize(proposal, byId, partySize, budget, maxItems);
            if (draft is null)
                continue;
            var fingerprint = Fingerprint(draft);
            if (!seen.Add(fingerprint))
                continue;
            accepted.Add(draft);
        }

        return accepted;
    }

    public static FoodCourse ResolveCourse(FoodItem food, string? proposed)
    {
        var parsed = ParseCourse(proposed);
        var known = food.Courses.Select(item => item.Course).ToHashSet();
        if (known.Count > 0)
        {
            if (parsed is not null && known.Contains(parsed.Value))
                return parsed.Value;
            var primary = food.Courses.FirstOrDefault(item => item.IsPrimary);
            return primary?.Course ?? food.Courses.First().Course;
        }

        return parsed ?? FoodCourse.EXTRA;
    }

    public static FoodCourse? ParseCourse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var token = raw.Trim().Replace('-', '_').Replace(' ', '_').ToUpperInvariant();
        token = token switch
        {
            "MAIN" or "MAINS" => nameof(FoodCourse.MAIN_COURSE),
            "SIDE" or "SIDES" => nameof(FoodCourse.SIDE_DISH),
            "SHARED" or "SHARE" or "SHARING" => nameof(FoodCourse.SHARED_DISH),
            _ => token
        };
        return Enum.TryParse<FoodCourse>(token, true, out var course) && Enum.IsDefined(course)
            ? course
            : null;
    }

    public static IReadOnlyList<FoodCourse> SectionOrder(IEnumerable<FoodCourse> present)
    {
        var extra = present
            .Where(course => !StandardCourses.Contains(course))
            .Distinct()
            .OrderBy(course => course.ToString());
        return StandardCourses.Concat(extra).ToArray();
    }

    private static AssistantMealPlanDraft? Materialize(
        AssistantMealPlanProposal proposal,
        IReadOnlyDictionary<Guid, AssistantScoredFood> byId,
        int partySize,
        decimal? budget,
        int maxItems)
    {
        var lines = new List<DraftLine>();
        foreach (var item in proposal.Items)
        {
            if (item.Quantity < 1)
                continue;
            if (!byId.TryGetValue(item.FoodItemId, out var scored))
                continue;
            var food = scored.Eligible.FoodItem;
            var quantity = Math.Max(1, item.Quantity);
            var existing = lines.Find(line => line.Food.Id == food.Id);
            if (existing is not null)
            {
                existing.Quantity = Math.Max(existing.Quantity, quantity);
                continue;
            }

            lines.Add(new DraftLine
            {
                Food = food,
                Scored = scored,
                Quantity = quantity,
                UnitPrice = scored.Eligible.EffectivePrice,
                Course = ResolveCourse(food, item.Course)
            });
        }

        if (lines.Count == 0)
            return null;

        var marketId = lines
            .GroupBy(line => line.Food.Booth.NightMarketId)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .First()
            .Key;
        lines = lines.Where(line => line.Food.Booth.NightMarketId == marketId).ToList();
        if (lines.Count == 0)
            return null;
        if (lines.Count > maxItems)
            lines = lines.Take(maxItems).ToList();

        var warnings = new List<string>();
        if (budget is not null && LineTotal(lines) > budget.Value)
        {
            lines = FitBudget(lines, budget.Value, warnings);
            if (lines.Count == 0)
                return null;
        }

        var total = LineTotal(lines);
        var market = lines[0].Food.Booth.NightMarket;
        var unknown = lines
            .SelectMany(line => line.Scored.UnknownDataFacets)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AssistantMealPlanDraft
        {
            NightMarketId = market.Id,
            NightMarketName = market.Name,
            EstimatedTotal = total,
            PartySize = partySize,
            BudgetMax = budget,
            RemainingBudget = budget is null ? null : budget.Value - total,
            Title = proposal.Title,
            Strategy = proposal.Strategy,
            OverallPlanReason = proposal.OverallReason,
            Warnings = warnings,
            UnknownDataFacets = unknown,
            Items = lines.Select(line => new AssistantMealPlanDraftItem
            {
                FoodItem = line.Food,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Course = line.Course
            }).ToArray()
        };
    }

    private static List<DraftLine> FitBudget(List<DraftLine> lines, decimal budget, List<string> warnings)
    {
        while (LineTotal(lines) > budget)
        {
            var reducible = lines
                .Where(line => line.Quantity > 1)
                .OrderByDescending(line => line.UnitPrice)
                .FirstOrDefault();
            if (reducible is null)
                break;
            reducible.Quantity -= 1;
            warnings.Add("BUDGET_QTY_REDUCED");
        }

        if (LineTotal(lines) <= budget)
            return DedupWarnings(lines, warnings);

        foreach (var course in DropOrder)
        {
            while (LineTotal(lines) > budget)
            {
                var drop = lines
                    .Where(line => line.Course == course)
                    .OrderBy(line => line.Scored.FinalScore)
                    .ThenByDescending(line => line.UnitPrice)
                    .FirstOrDefault();
                if (drop is null)
                    break;
                lines.Remove(drop);
                warnings.Add("BUDGET_ITEM_DROPPED");
                if (lines.Count == 0)
                    return [];
            }

            if (LineTotal(lines) <= budget)
                return DedupWarnings(lines, warnings);
        }

        return [];
    }

    private static List<DraftLine> DedupWarnings(List<DraftLine> lines, List<string> warnings)
    {
        var unique = warnings.Distinct(StringComparer.Ordinal).ToArray();
        warnings.Clear();
        warnings.AddRange(unique);
        return lines;
    }

    private static decimal LineTotal(IReadOnlyList<DraftLine> lines)
        => lines.Sum(line => line.UnitPrice * line.Quantity);

    private static string Fingerprint(AssistantMealPlanDraft draft)
        => string.Join("|", draft.Items
            .OrderBy(item => item.FoodItem.Id)
            .Select(item => $"{item.FoodItem.Id}:{item.Quantity}"));

    private sealed class DraftLine
    {
        public required FoodItem Food { get; init; }
        public required AssistantScoredFood Scored { get; init; }
        public required decimal UnitPrice { get; init; }
        public required FoodCourse Course { get; init; }
        public int Quantity { get; set; }
    }
}
