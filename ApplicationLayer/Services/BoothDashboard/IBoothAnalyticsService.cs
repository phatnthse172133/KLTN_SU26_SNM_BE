using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.BoothDashboard;

public interface IBoothAnalyticsService
{
    Task<ApiResponse<BoothAnalyticsResponse>> GetAnalyticsAsync(
        Guid boothOwnerId,
        DashboardPeriod period,
        CancellationToken ct = default);

    Task<ApiResponse<BoothRevenueSeriesResponse>> GetRevenueSeriesAsync(
        Guid boothOwnerId,
        int days,
        CancellationToken ct = default);

    Task<ApiResponse<BoothPromotionPerformanceResponse>> GetPromotionPerformanceAsync(
        Guid boothOwnerId,
        CancellationToken ct = default);
}
