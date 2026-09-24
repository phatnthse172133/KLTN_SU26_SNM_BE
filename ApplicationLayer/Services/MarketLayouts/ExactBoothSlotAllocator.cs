namespace ApplicationLayer.Services.MarketLayouts;

public sealed record BoothSlotAllocationResult(
    bool IsSuccessful,
    IReadOnlyList<int> Allocations,
    int RequestedCount,
    int AvailableCapacity,
    string? Error);

/// <summary>
/// Deterministic balanced allocator. It visits zones in configured order and
/// gives each non-full zone one slot per pass. Stable request order is the
/// primary order; callers must use a stable ID fallback before calling when
/// their source is not already ordered.
/// </summary>
public static class ExactBoothSlotAllocator
{
    public static BoothSlotAllocationResult Allocate(
        int requestedCount,
        IReadOnlyList<int> capacities)
    {
        var available = capacities.Where(capacity => capacity > 0).Sum();
        var allocations = new int[capacities.Count];
        if (requestedCount <= 0)
            return new(false, allocations, requestedCount, available,
                "Requested booth count must be a positive integer.");
        if (capacities.Any(capacity => capacity < 0))
            return new(false, allocations, requestedCount, available,
                "Zone physical capacities cannot be negative.");
        if (available < requestedCount)
            return new(false, allocations, requestedCount, available,
                $"Requested {requestedCount} booth slots, but the configured zones can physically contain only {available}.");

        var remaining = requestedCount;
        while (remaining > 0)
        {
            var progressed = false;
            for (var index = 0; index < capacities.Count && remaining > 0; index++)
            {
                if (allocations[index] >= capacities[index])
                    continue;
                allocations[index]++;
                remaining--;
                progressed = true;
            }
            if (!progressed)
                break;
        }

        var allocated = allocations.Sum();
        return allocated == requestedCount
            ? new(true, allocations, requestedCount, available, null)
            : new(false, allocations, requestedCount, available,
                $"Exact allocation failed: requested {requestedCount}, allocated {allocated}.");
    }
}
