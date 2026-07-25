using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Helppers;
using System;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Subscriptions
{
    public interface ISubscriptionService
    {
        Task<PaginationResp<AdminSubscriptionDto>> GetSubscriptionsAsync(PackageType? type, SubscriptionStatus? status, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
        Task<bool> VerifySubscriptionAsync(Guid subscriptionId, PackageType type, VerifySubscriptionRequest request);
    }
}
