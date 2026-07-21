using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using ApplicationLayer.Services.Subscriptions;
using ApplicationLayer.DTOs;
using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.Exceptions;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer
{
    public class SubscriptionEntitlementServiceTests
    {
        private readonly Mock<ISubscriptionRepository> _mockRepo;
        private readonly SubscriptionEntitlementService _service;

        public SubscriptionEntitlementServiceTests()
        {
            _mockRepo = new Mock<ISubscriptionRepository>();
            _service = new SubscriptionEntitlementService(_mockRepo.Object);
        }

        [Fact]
        public async Task GetMarketEntitlements_NoSubscription_ReturnsNone()
        {
            _mockRepo.Setup(r => r.GetActiveMarketSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MarketSubscription?)null);

            var result = await _service.GetMarketEntitlementsAsync(Guid.NewGuid());

            Assert.Equal(0, result.MaxMarkets);
            Assert.Equal(0, result.MaxSlotsPerMarket);
            Assert.False(result.ZoneManagement);
        }

        [Fact]
        public async Task GetMarketEntitlements_WithProPackage_ReturnsProEntitlements()
        {
            var sub = new MarketSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeMarket(MarketEntitlements.Pro)
                }
            };

            _mockRepo.Setup(r => r.GetActiveMarketSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var result = await _service.GetMarketEntitlementsAsync(Guid.NewGuid());

            Assert.Equal(3, result.MaxMarkets);
            Assert.True(result.ZoneManagement);
            Assert.True(result.AdvancedReports);
        }

        [Fact]
        public async Task GetBoothEntitlements_NoSubscription_ReturnsFree()
        {
            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BoothSubscription?)null);

            var result = await _service.GetBoothEntitlementsAsync(Guid.NewGuid());

            Assert.False(result.Promotion);
            Assert.False(result.FeaturedFood);
            Assert.Equal(1, result.RecommendationPriority);
        }

        [Fact]
        public async Task GetBoothEntitlements_WithFeaturedPackage_ReturnsFeaturedEntitlements()
        {
            var sub = new BoothSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeBooth(BoothEntitlements.Featured)
                }
            };

            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var result = await _service.GetBoothEntitlementsAsync(Guid.NewGuid());

            Assert.True(result.Promotion);
            Assert.True(result.FeaturedBooth);
            Assert.True(result.FeaturedFood);
            Assert.Equal(1.5, result.RecommendationPriority);
        }

        [Fact]
        public async Task GetBoothEntitlements_WithGrowthPackage_ReturnsGrowthEntitlements()
        {
            var sub = new BoothSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeBooth(BoothEntitlements.Growth)
                }
            };

            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var result = await _service.GetBoothEntitlementsAsync(Guid.NewGuid());

            Assert.True(result.Promotion);
            Assert.True(result.AdvancedAnalytics);
            Assert.False(result.FeaturedBooth);
            Assert.False(result.FeaturedFood);
        }

        [Fact]
        public async Task GetBoothEntitlements_WithInvalidJson_ReturnsFree()
        {
            var sub = new BoothSubscription
            {
                Package = new Package { Entitlements = "invalid json" }
            };

            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var result = await _service.GetBoothEntitlementsAsync(Guid.NewGuid());

            Assert.False(result.Promotion);
            Assert.Equal(1, result.RecommendationPriority);
        }

        [Fact]
        public async Task GetBoothEntitlements_WithNullEntitlements_ReturnsFree()
        {
            var sub = new BoothSubscription
            {
                Package = new Package { Entitlements = null }
            };

            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var result = await _service.GetBoothEntitlementsAsync(Guid.NewGuid());

            Assert.False(result.Promotion);
        }

        [Fact]
        public async Task RequireMarketFeature_FeatureNotAvailable_ThrowsForbidden()
        {
            _mockRepo.Setup(r => r.GetActiveMarketSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MarketSubscription?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RequireMarketFeatureAsync(Guid.NewGuid(), e => e.ZoneManagement, "Zone management required"));

            Assert.Equal(403, ex.StatusCode);
        }

        [Fact]
        public async Task RequireMarketFeature_FeatureAvailable_Succeeds()
        {
            var sub = new MarketSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeMarket(MarketEntitlements.Pro)
                }
            };

            _mockRepo.Setup(r => r.GetActiveMarketSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            await _service.RequireMarketFeatureAsync(Guid.NewGuid(), e => e.ZoneManagement, "Zone management required");
        }

        [Fact]
        public async Task RequireBoothFeature_FeatureNotAvailable_ThrowsForbidden()
        {
            var sub = new BoothSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeBooth(BoothEntitlements.Free)
                }
            };

            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RequireBoothFeatureAsync(Guid.NewGuid(), e => e.Promotion, "Promotion feature required"));

            Assert.Equal(403, ex.StatusCode);
            Assert.Equal("BOOTH_PACKAGE_REQUIRED", ex.ErrorCode);
        }

        [Fact]
        public async Task RequireBoothFeature_FeatureAvailable_Succeeds()
        {
            var sub = new BoothSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeBooth(BoothEntitlements.Growth)
                }
            };

            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            await _service.RequireBoothFeatureAsync(Guid.NewGuid(), e => e.Promotion, "Promotion feature required");
        }

        [Fact]
        public async Task GetMaxMarketsAsync_NoSubscription_Returns0()
        {
            _mockRepo.Setup(r => r.GetActiveMarketSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MarketSubscription?)null);

            var result = await _service.GetMaxMarketsAsync(Guid.NewGuid());

            Assert.Equal(0, result);
        }

        [Fact]
        public async Task GetMaxMarketsAsync_ProSubscription_Returns3()
        {
            var sub = new MarketSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeMarket(MarketEntitlements.Pro)
                }
            };

            _mockRepo.Setup(r => r.GetActiveMarketSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var result = await _service.GetMaxMarketsAsync(Guid.NewGuid());

            Assert.Equal(3, result);
        }

        [Fact]
        public async Task CanUseFeaturedFoodAsync_NoSubscription_ReturnsFalse()
        {
            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BoothSubscription?)null);

            var result = await _service.CanUseFeaturedFoodAsync(Guid.NewGuid());

            Assert.False(result);
        }

        [Fact]
        public async Task CanUseFeaturedFoodAsync_FeaturedSubscription_ReturnsTrue()
        {
            var sub = new BoothSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeBooth(BoothEntitlements.Featured)
                }
            };

            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var result = await _service.CanUseFeaturedFoodAsync(Guid.NewGuid());

            Assert.True(result);
        }

        [Fact]
        public async Task CanUsePromotionAsync_GrowthSubscription_ReturnsTrue()
        {
            var sub = new BoothSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeBooth(BoothEntitlements.Growth)
                }
            };

            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var result = await _service.CanUsePromotionAsync(Guid.NewGuid());

            Assert.True(result);
        }

        [Fact]
        public async Task CanUsePromotionAsync_FreeSubscription_ReturnsFalse()
        {
            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BoothSubscription?)null);

            var result = await _service.CanUsePromotionAsync(Guid.NewGuid());

            Assert.False(result);
        }

        [Fact]
        public async Task GetRecommendationPriorityAsync_NoSubscription_Returns1()
        {
            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BoothSubscription?)null);

            var result = await _service.GetRecommendationPriorityAsync(Guid.NewGuid());

            Assert.Equal(1, result);
        }

        [Fact]
        public async Task GetRecommendationPriorityAsync_FeaturedSubscription_Returns3()
        {
            var sub = new BoothSubscription
            {
                Package = new Package
                {
                    Entitlements = EntitlementHelper.SerializeBooth(BoothEntitlements.Featured)
                }
            };

            _mockRepo.Setup(r => r.GetActiveBoothSubscriptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sub);

            var result = await _service.GetRecommendationPriorityAsync(Guid.NewGuid());

            Assert.Equal(1.5, result);
        }
    }
}
