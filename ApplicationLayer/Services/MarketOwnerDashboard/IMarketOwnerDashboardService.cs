using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.MarketOwnerDashboard;

public interface IMarketOwnerDashboardService
{
    Task<ApiResponse<MarketOwnerDashboardResponse>> GetDashboardAsync(
        Guid marketOwnerId,
        Guid? marketId,
        DashboardPeriod period,
        CancellationToken ct = default);
}
