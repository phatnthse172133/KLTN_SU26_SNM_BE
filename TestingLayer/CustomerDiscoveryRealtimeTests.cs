using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.AdminModeration;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Realtime;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static DomainLayer.Enums.GeneralEnum;

namespace TestingLayer;

/// <summary>
/// Ensures Market/Booth status events fan out to role:Customer so discovery lists
/// (not joined to market:{id}) converge without F5.
/// </summary>
public class CustomerDiscoveryRealtimeTests
{
    [Fact]
    public async Task ChangeMarketModerationStatus_PublishesMarketStatusChanged_ToCustomerRole()
    {
        var market = new NightMarket
        {
            Id = Guid.NewGuid(),
            MarketOwnerId = Guid.NewGuid(),
            Name = "Discovery Market",
            Address = "1 Test St",
            Status = NightMarketStatus.Active,
            ModerationStatus = ModerationStatus.Active,
            UpdatedAt = DateTime.UtcNow
        };
        var moderationRepo = new Mock<IModerationRepository>();
        var realtime = new Mock<IRealtimeEventPublisher>();

        moderationRepo.Setup(r => r.GetMarketDetailAsync(market.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(market);
        moderationRepo.Setup(r => r.UpdateMarketModerationStatusAsync(
                market.Id, ModerationStatus.Active, ModerationStatus.Suspended, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new AdminModerationService(
            moderationRepo.Object,
            new Mock<INightMarketRepository>().Object,
            new Mock<IBoothRepository>().Object,
            new Mock<IUserRepository>().Object,
            new Mock<INotificationService>().Object,
            new Mock<ILogger<AdminModerationService>>().Object,
            realtime.Object);

        await service.ChangeMarketModerationStatusAsync(
            Guid.NewGuid(),
            "Admin",
            market.Id,
            new ChangeModerationStatusRequest
            {
                Status = ModerationStatus.Suspended,
                Reason = "Valid suspension reason for discovery realtime."
            });

        realtime.Verify(r => r.PublishAsync(
            It.Is<RealtimeEvent>(e =>
                e.EventType == "MarketStatusChanged"
                && e.Role == "Customer"
                && e.GroupName == RealtimeGroups.Market(market.Id)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task BanBooth_PublishesBoothStatusChanged_ToCustomerRole()
    {
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            NightMarketId = Guid.NewGuid(),
            BoothOwnerId = Guid.NewGuid(),
            BoothName = "Discovery Booth",
            BoothCode = "D1",
            Status = BoothStatus.Active,
            UpdatedAt = DateTime.UtcNow
        };
        var moderationRepo = new Mock<IModerationRepository>();
        var realtime = new Mock<IRealtimeEventPublisher>();

        moderationRepo.Setup(r => r.GetBoothDetailAsync(booth.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booth);
        moderationRepo.Setup(r => r.UpdateBoothStatusAsync(
                booth.Id, BoothStatus.Active, BoothStatus.Banned, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new AdminModerationService(
            moderationRepo.Object,
            new Mock<INightMarketRepository>().Object,
            new Mock<IBoothRepository>().Object,
            new Mock<IUserRepository>().Object,
            new Mock<INotificationService>().Object,
            new Mock<ILogger<AdminModerationService>>().Object,
            realtime.Object);

        await service.BanBoothAsync(
            Guid.NewGuid(),
            "Admin",
            booth.Id,
            new BoothModerationActionRequest { Reason = "Valid ban reason for discovery realtime." });

        realtime.Verify(r => r.PublishAsync(
            It.Is<RealtimeEvent>(e =>
                e.EventType == "BoothStatusChanged"
                && e.Role == "Customer"
                && e.GroupName == RealtimeGroups.Booth(booth.Id)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
