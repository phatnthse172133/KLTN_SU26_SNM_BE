using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.BoothDashboard;

public interface IBoothDashboardService
{
    Task<ApiResponse<BoothDashboardResponse>> GetDashboardAsync(
        Guid boothOwnerId,
        DashboardPeriod period,
        CancellationToken ct = default);
}
