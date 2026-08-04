using System.Text;
using System.Text.Json;
using InfrastructureLayer.Data.Migrations;

namespace InfrastructureLayer.Data.Backfill;

public enum AiV2BackfillMode { DryRun, Execute }

public sealed record AiV2BackfillOptions(AiV2BackfillMode Mode, int BatchSize = 200)
{
    public void Validate()
    {
        if (BatchSize is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(BatchSize));
    }
}

public sealed record AiV2BackfillException(
    string Code,
    string RelationType,
    Guid? RelationId,
    string Reason);

public sealed record AiV2DataQualityReport(
    int FoodsWithoutCourse,
    int FoodsWithoutServingCount,
    int FoodsWithoutSemanticDescription,
    int FoodsWithMultiplePrimaryCourses,
    int CustomerHardSoftConflicts,
    IReadOnlyList<Guid> ContradictoryFoodSemanticIds);

public sealed record AiV2BackfillReport(
    AiV2BackfillMode Mode,
    int BatchSize,
    DateTime GeneratedAtUtc,
    int TotalLegacyTags,
    int SystemTags,
    int NonSystemTags,
    int ActiveTags,
    int ArchivedTags,
    int FoodItemTagBefore,
    int CustomerPreferenceBefore,
    IReadOnlyDictionary<string, int> SpecialRelationCounts,
    IReadOnlyDictionary<string, int> CatalogDispositionCounts,
    IReadOnlyDictionary<string, int> RelationDispositionCounts,
    int CoveredLegacyRelations,
    IReadOnlyDictionary<string, int> TargetCounts,
    IReadOnlyDictionary<string, int> PlannedWrites,
    IReadOnlyDictionary<string, int> FinalNormalizedCounts,
    int UnmappedRelations,
    int AmbiguousRelations,
    IReadOnlyList<AiV2BackfillException> Exceptions,
    IReadOnlyList<SoupMigrationResolution> SoupResolutions,
    AiV2DataQualityReport DataQuality,
    bool LegacyDataChanged,
    bool TransactionCommitted)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public string ToMarkdown()
    {
        var text = new StringBuilder();
        text.AppendLine("# AI V2 backfill reconciliation");
        text.AppendLine();
        text.AppendLine($"- Mode: `{Mode}`");
        text.AppendLine($"- Generated (UTC): `{GeneratedAtUtc:O}`");
        text.AppendLine($"- Legacy tags: {TotalLegacyTags} ({SystemTags} system, {NonSystemTags} non-system, {ActiveTags} active, {ArchivedTags} archived)");
        text.AppendLine($"- Legacy relations preserved: {FoodItemTagBefore} FoodItemTag, {CustomerPreferenceBefore} CustomerPreference");
        foreach (var item in SpecialRelationCounts.OrderBy(value => value.Key)) text.AppendLine($"- {item.Key}: {item.Value}");
        text.AppendLine($"- Unmapped: {UnmappedRelations}; ambiguous/manual: {AmbiguousRelations}");
        text.AppendLine($"- Legacy data changed: {LegacyDataChanged}");
        text.AppendLine();
        text.AppendLine("## Planned writes");
        foreach (var item in PlannedWrites.OrderBy(value => value.Key)) text.AppendLine($"- {item.Key}: {item.Value}");
        text.AppendLine();
        text.AppendLine("## Exceptions");
        foreach (var item in Exceptions) text.AppendLine($"- `{item.Code}` / `{item.RelationType}` / `{item.RelationId}`: {item.Reason}");
        text.AppendLine();
        text.AppendLine("## SOUP resolutions");
        foreach (var item in SoupResolutions) text.AppendLine($"- `{item.FoodItemId}` {item.FoodName}: **{item.ResolutionStatus}** ({item.Confidence:P0}) — {item.Evidence}");
        return text.ToString();
    }
}
