using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

public class PromotionUsageLifecycleTests
{
    private static readonly DateTime UtcNow = new(
        2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ConsumeReserved_IsIdempotent()
    {
        var usage = CreateUsage(PromotionUsageStatus.Reserved);

        Assert.Equal(1, PromotionUsageLifecycle.ConsumeReserved([usage], UtcNow));
        Assert.Equal(0, PromotionUsageLifecycle.ConsumeReserved([usage], UtcNow.AddMinutes(1)));
        Assert.Equal(PromotionUsageStatus.Consumed, usage.Status);
        Assert.Equal(UtcNow, usage.UpdatedAt);
        Assert.Null(usage.ReleasedAt);
    }

    [Fact]
    public void ReleaseReserved_MakesQuotaReusableAndIsIdempotent()
    {
        var usage = CreateUsage(PromotionUsageStatus.Reserved);

        Assert.Equal(1, PromotionUsageLifecycle.ReleaseReserved([usage], UtcNow));
        Assert.Equal(0, PromotionUsageLifecycle.ReleaseReserved([usage], UtcNow.AddMinutes(1)));
        Assert.Equal(PromotionUsageStatus.Released, usage.Status);
        Assert.Equal(UtcNow, usage.ReleasedAt);
        Assert.Equal(UtcNow, usage.UpdatedAt);
    }

    [Fact]
    public void PaidConsumedUsage_IsNeverReleasedByLaterCancellation()
    {
        var usage = CreateUsage(PromotionUsageStatus.Consumed);

        var changed = PromotionUsageLifecycle.ReleaseReserved([usage], UtcNow);

        Assert.Equal(0, changed);
        Assert.Equal(PromotionUsageStatus.Consumed, usage.Status);
        Assert.Null(usage.ReleasedAt);
    }

    [Fact]
    public void ReleasedUsage_IsNeverConsumedByLatePayment()
    {
        var usage = CreateUsage(PromotionUsageStatus.Released);
        usage.ReleasedAt = UtcNow.AddMinutes(-1);

        var changed = PromotionUsageLifecycle.ConsumeReserved([usage], UtcNow);

        Assert.Equal(0, changed);
        Assert.Equal(PromotionUsageStatus.Released, usage.Status);
        Assert.NotNull(usage.ReleasedAt);
    }

    private static PromotionUsage CreateUsage(PromotionUsageStatus status)
        => new()
        {
            Id = Guid.NewGuid(),
            PromotionId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Status = status,
            AppliedAt = UtcNow.AddMinutes(-5),
            CreatedAt = UtcNow.AddMinutes(-5),
            UpdatedAt = UtcNow.AddMinutes(-5)
        };
}
