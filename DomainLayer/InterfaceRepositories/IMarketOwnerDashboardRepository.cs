using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IMarketOwnerDashboardRepository
{
    Task<List<Guid>> GetOwnedMarketIdsAsync(Guid marketOwnerId, CancellationToken ct = default);
    Task<bool> IsMarketOwnedByAsync(Guid marketId, Guid marketOwnerId, CancellationToken ct = default);
    Task<int> CountNightMarketsAsync(Guid marketOwnerId, CancellationToken ct = default);
    Task<int> CountActiveBoothsAsync(List<Guid> marketIds, Guid? marketId, CancellationToken ct = default);
    Task<ComplaintStatusCounts> CountComplaintStatusesAsync(List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<BoothStatusCounts> CountBoothStatusesAsync(List<Guid> marketIds, Guid? marketId, CancellationToken ct = default);
    Task<int> CountValidOrdersAsync(List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<List<OrderTrendRow>> GetOrderTrendAsync(List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, string granularity, CancellationToken ct = default);
    Task<List<PeakHourRow>> GetPeakHoursAsync(List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<List<ZoneActivityRow>> GetZoneActivityAsync(List<Guid> marketIds, Guid? marketId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public record ComplaintStatusCounts(int Pending, int Resolved, int Rejected);
public record BoothStatusCounts(int Active, int Inactive, int Suspended, int Closed);
public record OrderTrendRow(DateTime BucketStart, int OrderCount);
public record PeakHourRow(int Hour, int OrderCount);
public record ZoneActivityRow(string ZoneName, int OrderCount);
