using DomainLayer.Common;
using DomainLayer.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository
{
    public interface ISubscriptionRepository
    {
        Task<PagedResult<SubscriptionView>> GetAdminSubscriptionsAsync(PackageType? type, SubscriptionStatus? status, int pageIndex, int pageSize, CancellationToken ct = default);
        Task<BoothSubscription?> GetBoothSubscriptionByIdAsync(Guid id, CancellationToken ct = default);
        Task<MarketSubscription?> GetMarketSubscriptionByIdAsync(Guid id, CancellationToken ct = default);
        Task<Package?> GetPackageByIdAsync(Guid id, CancellationToken ct = default);
        Task<Package?> GetPackageByCodeAsync(string code, CancellationToken ct = default);
        Task<PackagePolicy?> GetActivePackagePolicyAsync(Guid packageId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);

        Task<BoothSubscription?> GetActiveBoothSubscriptionAsync(Guid boothId, CancellationToken ct = default);
        Task<BoothSubscription?> GetLatestApprovedBoothSubscriptionAsync(Guid boothId, CancellationToken ct = default);
        Task<MarketSubscription?> GetActiveMarketSubscriptionAsync(Guid marketOwnerId, CancellationToken ct = default);
        Task<MarketSubscription?> GetLatestApprovedMarketSubscriptionAsync(Guid marketOwnerId, CancellationToken ct = default);
        Task<bool> HasPendingBoothSubscriptionAsync(Guid boothId, CancellationToken ct = default);
        Task<bool> HasPendingMarketSubscriptionAsync(Guid marketOwnerId, CancellationToken ct = default);
        Task<MarketSubscription?> GetPendingMarketSubscriptionAsync(Guid marketOwnerId, CancellationToken ct = default);
        Task<BoothSubscription?> GetPendingBoothSubscriptionAsync(Guid boothId, CancellationToken ct = default);
        Task<List<BoothSubscription>> GetBoothSubscriptionHistoryAsync(Guid boothId, CancellationToken ct = default);
        Task<List<MarketSubscription>> GetMarketSubscriptionHistoryAsync(Guid marketOwnerId, CancellationToken ct = default);
        Task AddBoothSubscriptionAsync(BoothSubscription subscription, CancellationToken ct = default);
        Task AddMarketSubscriptionAsync(MarketSubscription subscription, CancellationToken ct = default);
        Task<PackagePrice?> GetPackagePriceByIdAsync(Guid packagePriceId, CancellationToken ct = default);
        Task<List<PackagePrice>> GetActivePricesForPackageAsync(Guid packageId, CancellationToken ct = default);
        Task BeginTransactionAsync(CancellationToken ct = default);
        Task CommitTransactionAsync(CancellationToken ct = default);
        Task RollbackTransactionAsync(CancellationToken ct = default);
        Task<int> UpdateBoothSubscriptionStatusAsync(Guid subscriptionId, SubscriptionStatus expectedStatus, SubscriptionStatus newStatus, DateTime startDate, DateTime endDate, string? adminNotes, CancellationToken ct = default);
        Task<int> UpdateMarketSubscriptionStatusAsync(Guid subscriptionId, SubscriptionStatus expectedStatus, SubscriptionStatus newStatus, DateTime startDate, DateTime endDate, string? adminNotes, CancellationToken ct = default);
        Task<int> CancelActiveBoothSubscriptionsAsync(Guid boothId, CancellationToken ct = default);
        Task<int> CancelActiveMarketSubscriptionsAsync(Guid marketOwnerId, CancellationToken ct = default);
        Task<BoothSubscription?> GetBoothSubscriptionByOrderCodeAsync(long orderCode, CancellationToken ct = default);
        Task<MarketSubscription?> GetMarketSubscriptionByOrderCodeAsync(long orderCode, CancellationToken ct = default);
        Task<int> ActivateBoothSubscriptionAsync(Guid subscriptionId, DateTime startDate, DateTime endDate, DateTime paidAt, CancellationToken ct = default);
        Task<int> ActivateMarketSubscriptionAsync(Guid subscriptionId, DateTime startDate, DateTime endDate, DateTime paidAt, CancellationToken ct = default);
        Task<int> CancelBoothSubscriptionAsync(Guid subscriptionId, CancellationToken ct = default);
        Task<int> CancelMarketSubscriptionAsync(Guid subscriptionId, CancellationToken ct = default);

        Task<PackagePolicy?> GetPackagePolicyByIdAsync(Guid id, CancellationToken ct = default);
        Task<List<PackagePolicy>> GetPackagePoliciesByPackageAsync(Guid packageId, CancellationToken ct = default);
        Task AddPackagePolicyAsync(PackagePolicy policy, CancellationToken ct = default);
        Task<int> DeactivateActivePolicyAsync(Guid packageId, CancellationToken ct = default);
        Task<int> ActivatePolicyAsync(Guid policyId, CancellationToken ct = default);
        Task<List<BoothSubscription>> GetExpiredBoothSubscriptionsAsync(CancellationToken ct = default);
        Task<List<MarketSubscription>> GetExpiredMarketSubscriptionsAsync(CancellationToken ct = default);
    }
}
