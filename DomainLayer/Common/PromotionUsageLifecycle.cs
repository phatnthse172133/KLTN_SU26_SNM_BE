using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Common;

/// <summary>
/// Idempotent in-memory transitions used when an Order aggregate is already tracked.
/// Database-side transitions use the same Reserved-only rule.
/// </summary>
public static class PromotionUsageLifecycle
{
    public static int ConsumeReserved(
        IEnumerable<PromotionUsage> usages,
        DateTime utcNow)
    {
        var changed = 0;
        foreach (var usage in usages.Where(usage =>
                     usage.Status == PromotionUsageStatus.Reserved))
        {
            usage.Status = PromotionUsageStatus.Consumed;
            usage.UpdatedAt = utcNow;
            changed++;
        }

        return changed;
    }

    public static int ReleaseReserved(
        IEnumerable<PromotionUsage> usages,
        DateTime utcNow)
    {
        var changed = 0;
        foreach (var usage in usages.Where(usage =>
                     usage.Status == PromotionUsageStatus.Reserved))
        {
            usage.Status = PromotionUsageStatus.Released;
            usage.ReleasedAt = utcNow;
            usage.UpdatedAt = utcNow;
            changed++;
        }

        return changed;
    }
}
