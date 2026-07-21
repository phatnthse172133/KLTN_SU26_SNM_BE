using ApplicationLayer.DTOs;
using DomainLayer.InterfaceRepository;
using System;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Subscriptions
{
    public interface ISubscriptionEntitlementService
    {
        Task<MarketEntitlements> GetMarketEntitlementsAsync(Guid marketOwnerId);
        Task<BoothEntitlements> GetBoothEntitlementsAsync(Guid boothId);
        Task<bool> HasActiveMarketSubscriptionAsync(Guid marketOwnerId);
        Task<bool> HasActiveBoothSubscriptionAsync(Guid boothId);
        Task RequireMarketFeatureAsync(Guid marketOwnerId, Func<MarketEntitlements, bool> check, string errorMessage, string errorCode = "MARKET_PACKAGE_REQUIRED");
        Task RequireBoothFeatureAsync(Guid boothId, Func<BoothEntitlements, bool> check, string errorMessage, string errorCode = "BOOTH_PACKAGE_REQUIRED");
        Task<int> GetMaxMarketsAsync(Guid marketOwnerId);
        Task<int> GetMaxSlotsPerMarketAsync(Guid marketOwnerId);
        Task<int?> GetMaxLayoutsPerMarketAsync(Guid marketOwnerId);
        Task<bool> CanUseZoneManagementAsync(Guid marketOwnerId);
        Task<bool> CanUseAdvancedReportsAsync(Guid marketOwnerId);
        Task<bool> CanUseAiInsightsAsync(Guid marketOwnerId);
        Task<bool> CanUseExportReportsAsync(Guid marketOwnerId);
        Task<bool> CanUseAdvancedComplaintAsync(Guid marketOwnerId);
        Task<bool> CanUseAdvancedBoothApprovalAsync(Guid marketOwnerId);
        Task<bool> CanUsePromotionAsync(Guid boothId);
        Task<bool> CanUseAdvancedAnalyticsAsync(Guid boothId);
        Task<double> GetRecommendationPriorityAsync(Guid boothId);
        Task<bool> CanUseFeaturedBoothAsync(Guid boothId);
        Task<bool> CanUseFeaturedFoodAsync(Guid boothId);
    }

    public class SubscriptionEntitlementService : ISubscriptionEntitlementService
    {
        private readonly ISubscriptionRepository _subscriptionRepo;

        public SubscriptionEntitlementService(ISubscriptionRepository subscriptionRepo)
        {
            _subscriptionRepo = subscriptionRepo;
        }

        public async Task<MarketEntitlements> GetMarketEntitlementsAsync(Guid marketOwnerId)
        {
            var sub = await _subscriptionRepo.GetActiveMarketSubscriptionAsync(marketOwnerId);
            if (sub?.Package is null)
                return MarketEntitlements.None;

            try
            {
                return EntitlementHelper.DeserializeMarketStrict(sub.Package.Entitlements);
            }
            catch (System.Text.Json.JsonException)
            {
                return MarketEntitlements.None;
            }
        }

        public async Task<BoothEntitlements> GetBoothEntitlementsAsync(Guid boothId)
        {
            var sub = await _subscriptionRepo.GetActiveBoothSubscriptionAsync(boothId);
            if (sub?.Package is null)
                return BoothEntitlements.Free;

            try
            {
                return EntitlementHelper.DeserializeBoothStrict(sub.Package.Entitlements);
            }
            catch (System.Text.Json.JsonException)
            {
                return BoothEntitlements.Free;
            }
        }

        public async Task<bool> HasActiveMarketSubscriptionAsync(Guid marketOwnerId)
        {
            var sub = await _subscriptionRepo.GetActiveMarketSubscriptionAsync(marketOwnerId);
            return sub != null;
        }

        public async Task<bool> HasActiveBoothSubscriptionAsync(Guid boothId)
        {
            var sub = await _subscriptionRepo.GetActiveBoothSubscriptionAsync(boothId);
            return sub != null;
        }

        public async Task RequireMarketFeatureAsync(Guid marketOwnerId, Func<MarketEntitlements, bool> check, string errorMessage, string errorCode = "MARKET_PACKAGE_REQUIRED")
        {
            var hasSub = await HasActiveMarketSubscriptionAsync(marketOwnerId);
            if (!hasSub)
                throw ApplicationLayer.Exceptions.AppException.Forbidden("An active Market subscription is required to perform this action.", "MARKET_SUBSCRIPTION_REQUIRED");

            var entitlements = await GetMarketEntitlementsAsync(marketOwnerId);
            if (!check(entitlements))
                throw ApplicationLayer.Exceptions.AppException.Forbidden(errorMessage, errorCode);
        }

        public async Task RequireBoothFeatureAsync(Guid boothId, Func<BoothEntitlements, bool> check, string errorMessage, string errorCode = "BOOTH_PACKAGE_REQUIRED")
        {
            var hasSub = await HasActiveBoothSubscriptionAsync(boothId);
            if (!hasSub)
                throw ApplicationLayer.Exceptions.AppException.Forbidden("An active Booth subscription is required to perform this action.", "BOOTH_SUBSCRIPTION_REQUIRED");

            var entitlements = await GetBoothEntitlementsAsync(boothId);
            if (!check(entitlements))
                throw ApplicationLayer.Exceptions.AppException.Forbidden(errorMessage, errorCode);
        }

        public async Task<int> GetMaxMarketsAsync(Guid marketOwnerId)
            => (await GetMarketEntitlementsAsync(marketOwnerId)).MaxMarkets;

        public async Task<int> GetMaxSlotsPerMarketAsync(Guid marketOwnerId)
            => (await GetMarketEntitlementsAsync(marketOwnerId)).MaxSlotsPerMarket;

        public async Task<int?> GetMaxLayoutsPerMarketAsync(Guid marketOwnerId)
            => (await GetMarketEntitlementsAsync(marketOwnerId)).MaxLayoutsPerMarket;

        public async Task<bool> CanUseZoneManagementAsync(Guid marketOwnerId)
            => (await GetMarketEntitlementsAsync(marketOwnerId)).ZoneManagement;

        public async Task<bool> CanUseAdvancedReportsAsync(Guid marketOwnerId)
            => (await GetMarketEntitlementsAsync(marketOwnerId)).AdvancedReports;

        public async Task<bool> CanUseAiInsightsAsync(Guid marketOwnerId)
            => (await GetMarketEntitlementsAsync(marketOwnerId)).AiInsights;

        public async Task<bool> CanUseExportReportsAsync(Guid marketOwnerId)
            => (await GetMarketEntitlementsAsync(marketOwnerId)).ExportReports;

        public async Task<bool> CanUseAdvancedComplaintAsync(Guid marketOwnerId)
            => (await GetMarketEntitlementsAsync(marketOwnerId)).AdvancedComplaint;

        public async Task<bool> CanUseAdvancedBoothApprovalAsync(Guid marketOwnerId)
            => (await GetMarketEntitlementsAsync(marketOwnerId)).AdvancedBoothApproval;

        public async Task<bool> CanUsePromotionAsync(Guid boothId)
            => (await GetBoothEntitlementsAsync(boothId)).Promotion;

        public async Task<bool> CanUseAdvancedAnalyticsAsync(Guid boothId)
            => (await GetBoothEntitlementsAsync(boothId)).AdvancedAnalytics;

        public async Task<double> GetRecommendationPriorityAsync(Guid boothId)
            => (await GetBoothEntitlementsAsync(boothId)).RecommendationPriority;

        public async Task<bool> CanUseFeaturedBoothAsync(Guid boothId)
            => (await GetBoothEntitlementsAsync(boothId)).FeaturedBooth;

        public async Task<bool> CanUseFeaturedFoodAsync(Guid boothId)
            => (await GetBoothEntitlementsAsync(boothId)).FeaturedFood;
    }
}
