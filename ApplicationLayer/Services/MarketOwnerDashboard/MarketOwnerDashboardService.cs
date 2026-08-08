using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Subscriptions;
using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.MarketOwnerDashboard;

public class MarketOwnerDashboardService : IMarketOwnerDashboardService
{
    private readonly IMarketOwnerDashboardRepository _repo;
    private readonly ISubscriptionEntitlementService _entitlements;

    private static readonly TimeZoneInfo VnZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public MarketOwnerDashboardService(
        IMarketOwnerDashboardRepository repo,
        ISubscriptionEntitlementService entitlements)
    {
        _repo = repo;
        _entitlements = entitlements;
    }

    public async Task<ApiResponse<MarketOwnerDashboardResponse>> GetDashboardAsync(
        Guid marketOwnerId,
        Guid? marketId,
        DashboardPeriod period,
        CancellationToken ct = default)
    {
        var hasSubscription = await _entitlements.HasActiveMarketSubscriptionAsync(marketOwnerId);
        if (!hasSubscription)
            throw AppException.Forbidden("An active Market subscription is required to access the dashboard.", "MARKET_SUBSCRIPTION_REQUIRED");

        if (!Enum.IsDefined(period))
            throw AppException.BadRequest("Invalid dashboard period.", "INVALID_DASHBOARD_PERIOD");

        var ownedMarkets = await _repo.GetOwnedMarketIdsAsync(marketOwnerId, ct);

        if (marketId.HasValue && !ownedMarkets.Contains(marketId.Value))
            throw AppException.Forbidden("This market does not belong to you.", "MARKET_NOT_OWNED");

        if (ownedMarkets.Count == 0)
            return await BuildEmptyDashboardAsync(marketOwnerId, period);

        var (fromUtc, toUtc, granularity) = ComputeRange(period);
        var periodLabel = period.ToString().ToLowerInvariant();

        // Entitlement checks
        var advancedReportsEnabled = await _entitlements.CanUseAdvancedReportsAsync(marketOwnerId);
        var aiInsightsEnabled = await _entitlements.CanUseAiInsightsAsync(marketOwnerId);
        var zoneInsightsEnabled = aiInsightsEnabled && await _entitlements.CanUseZoneManagementAsync(marketOwnerId);

        // Basic summary â€” always available
        var nightMarketsCount = await _repo.CountNightMarketsAsync(marketOwnerId, ct);
        var activeBooths = await _repo.CountActiveBoothsAsync(ownedMarkets, marketId, ct);

        var summary = new DashboardSummary
        {
            NightMarkets = nightMarketsCount,
            ActiveBooths = activeBooths,
            ValidOrders = null,
            PendingComplaints = null
        };

        // Pro (advancedReports) data â€” only query if entitled
        List<OrderTrendBucket>? orderTrend = null;
        ComplaintStatusBreakdown? complaintStatus = null;
        BoothStatusBreakdown? boothStatus = null;

        if (advancedReportsEnabled)
        {
            var validOrders = await _repo.CountValidOrdersAsync(ownedMarkets, marketId, fromUtc, toUtc, ct);
            var complaintCounts = await _repo.CountComplaintStatusesAsync(ownedMarkets, marketId, fromUtc, toUtc, ct);
            summary.ValidOrders = validOrders;
            summary.PendingComplaints = complaintCounts.Pending;

            var trendData = await _repo.GetOrderTrendAsync(ownedMarkets, marketId, fromUtc, toUtc, granularity, ct);
            orderTrend = BuildTrendBuckets(trendData, fromUtc, toUtc, granularity);

            complaintStatus = new ComplaintStatusBreakdown
            {
                Pending = complaintCounts.Pending,
                Resolved = complaintCounts.Resolved,
                Rejected = complaintCounts.Rejected
            };

            var boothCounts = await _repo.CountBoothStatusesAsync(ownedMarkets, marketId, ct);
            boothStatus = new BoothStatusBreakdown
            {
                Active = boothCounts.Active,
                Inactive = boothCounts.Inactive,
                Suspended = boothCounts.Suspended,
                Closed = boothCounts.Closed
            };
        }

        // Pro (aiInsights) data â€” only query if entitled
        AdvancedInsights? advanced = null;
        if (aiInsightsEnabled)
        {
            advanced = new AdvancedInsights
            {
                PeakHours = new List<PeakHourBucket>(),
                ZoneActivity = null
            };

            var peakHours = await _repo.GetPeakHoursAsync(ownedMarkets, marketId, fromUtc, toUtc, ct);
            advanced.PeakHours = peakHours
                .Select(p => new PeakHourBucket
                {
                    Hour = p.Hour,
                    Label = $"{p.Hour:00}:00",
                    OrderCount = p.OrderCount
                })
                .ToList();

            if (zoneInsightsEnabled)
            {
                var zoneActivity = await _repo.GetZoneActivityAsync(ownedMarkets, marketId, fromUtc, toUtc, ct);
                advanced.ZoneActivity = zoneActivity
                    .Select(z => new ZoneActivityBucket
                    {
                        ZoneName = z.ZoneName,
                        OrderCount = z.OrderCount
                    })
                    .ToList();
            }
        }

        var response = new MarketOwnerDashboardResponse
        {
            Range = new DashboardRangeInfo
            {
                Period = periodLabel,
                FromDate = fromUtc,
                ToDate = toUtc,
                Granularity = granularity
            },
            Summary = summary,
            Entitlements = new DashboardEntitlements
            {
                AdvancedReportsEnabled = advancedReportsEnabled,
                AiInsightsEnabled = aiInsightsEnabled,
                ZoneInsightsEnabled = zoneInsightsEnabled
            },
            OrderTrend = orderTrend,
            ComplaintStatus = complaintStatus,
            BoothStatus = boothStatus,
            Advanced = advanced
        };

        return ApiResponse<MarketOwnerDashboardResponse>.SuccessResponse(response);
    }

    private async Task<ApiResponse<MarketOwnerDashboardResponse>> BuildEmptyDashboardAsync(
        Guid marketOwnerId, DashboardPeriod period)
    {
        var (fromUtc, toUtc, granularity) = ComputeRange(period);
        var periodLabel = period.ToString().ToLowerInvariant();

        var advancedReportsEnabled = await _entitlements.CanUseAdvancedReportsAsync(marketOwnerId);
        var aiInsightsEnabled = await _entitlements.CanUseAiInsightsAsync(marketOwnerId);
        var zoneInsightsEnabled = aiInsightsEnabled && await _entitlements.CanUseZoneManagementAsync(marketOwnerId);

        return ApiResponse<MarketOwnerDashboardResponse>.SuccessResponse(new MarketOwnerDashboardResponse
        {
            Range = new DashboardRangeInfo
            {
                Period = periodLabel,
                FromDate = fromUtc,
                ToDate = toUtc,
                Granularity = granularity
            },
            Summary = new DashboardSummary
            {
                NightMarkets = 0,
                ActiveBooths = 0,
                ValidOrders = null,
                PendingComplaints = null
            },
            Entitlements = new DashboardEntitlements
            {
                AdvancedReportsEnabled = advancedReportsEnabled,
                AiInsightsEnabled = aiInsightsEnabled,
                ZoneInsightsEnabled = zoneInsightsEnabled
            },
            OrderTrend = null,
            ComplaintStatus = null,
            BoothStatus = null,
            Advanced = null
        });
    }

    private static (DateTime fromUtc, DateTime toUtc, string granularity) ComputeRange(DashboardPeriod period)
    {
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnZone);

        switch (period)
        {
            case DashboardPeriod.Week:
            {
                var toVn = nowVn.Date.AddDays(1);
                var fromVn = toVn.AddDays(-7);
                return (ToUtc(fromVn), ToUtc(toVn), "day");
            }
            case DashboardPeriod.Month:
            {
                var toVn = nowVn.Date.AddDays(1);
                var fromVn = toVn.AddDays(-30);
                return (ToUtc(fromVn), ToUtc(toVn), "day");
            }
            case DashboardPeriod.Year:
            {
                var toVn = new DateTime(nowVn.Year, nowVn.Month, 1).AddMonths(1);
                var fromVn = toVn.AddYears(-1);
                return (ToUtc(fromVn), ToUtc(toVn), "month");
            }
            default:
                throw AppException.BadRequest("Invalid dashboard period.", "INVALID_DASHBOARD_PERIOD");
        }
    }

    private static DateTime ToUtc(DateTime vnTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(vnTime, DateTimeKind.Unspecified), VnZone);
    }

    private static List<OrderTrendBucket> BuildTrendBuckets(
        List<OrderTrendRow> data, DateTime fromUtc, DateTime toUtc, string granularity)
    {
        var result = new List<OrderTrendBucket>();
        var lookup = data.ToDictionary(d => d.BucketStart, d => d.OrderCount);

        var fromVn = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, VnZone);
        var toVn = TimeZoneInfo.ConvertTimeFromUtc(toUtc, VnZone);

        if (granularity == "month")
        {
            var cursor = new DateTime(fromVn.Year, fromVn.Month, 1);
            var end = new DateTime(toVn.Year, toVn.Month, 1);
            while (cursor < end)
            {
                result.Add(new OrderTrendBucket
                {
                    BucketStart = cursor,
                    Label = cursor.ToString("MMM yy", CultureInfo.InvariantCulture),
                    OrderCount = lookup.TryGetValue(cursor, out var c) ? c : 0
                });
                cursor = cursor.AddMonths(1);
            }
        }
        else
        {
            var cursor = fromVn.Date;
            var end = toVn.Date;
            while (cursor < end)
            {
                result.Add(new OrderTrendBucket
                {
                    BucketStart = cursor,
                    Label = cursor.ToString("MMM dd", CultureInfo.InvariantCulture),
                    OrderCount = lookup.TryGetValue(cursor, out var c) ? c : 0
                });
                cursor = cursor.AddDays(1);
            }
        }

        return result;
    }
}
