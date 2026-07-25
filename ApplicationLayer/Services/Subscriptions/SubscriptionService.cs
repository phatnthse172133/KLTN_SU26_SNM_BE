using ApplicationLayer.DTOs.Admin;
using ApplicationLayer.DTOs.Subscriptions;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Subscriptions
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly INotificationService _notifications;

        public SubscriptionService(ISubscriptionRepository subscriptionRepository, INotificationService notifications)
        {
            _subscriptionRepository = subscriptionRepository;
            _notifications = notifications;
        }

        public async Task<PaginationResp<AdminSubscriptionDto>> GetSubscriptionsAsync(PackageType? type, SubscriptionStatus? status, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
        {
            var result = await _subscriptionRepository.GetAdminSubscriptionsAsync(type, status, pageIndex, pageSize, cancellationToken);

            var items = result.Items.Select(s => new AdminSubscriptionDto
            {
                Id = s.Id,
                OwnerId = s.OwnerId,
                OwnerName = s.OwnerName,
                OwnerEmail = s.OwnerEmail,
                PackageType = s.PackageType,
                PackageId = s.PackageId,
                PackageName = s.PackageName,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                Status = s.Status,
                AdminNotes = s.AdminNotes,
                PaidAmount = s.PaidAmount,
                PayOSOrderCode = s.PayOSOrderCode,
                PaidAt = s.PaidAt,
                CreatedAt = s.CreatedAt,
                BuyerName = s.BuyerName,
                BuyerEmail = s.BuyerEmail,
                BuyerPhone = s.BuyerPhone
            }).ToList();

            return PaginationResp<AdminSubscriptionDto>.Create(items, result.TotalCount, new PaginationReq { Page = pageIndex, PageSize = pageSize });
        }

        public async Task<bool> VerifySubscriptionAsync(Guid subscriptionId, PackageType type, VerifySubscriptionRequest request)
        {
            if (type != PackageType.Booth && type != PackageType.Market)
                throw AppException.BadRequest("Invalid package type. Only Booth(0) and Market(1) are allowed.");

            if (!request.IsApproved && string.IsNullOrWhiteSpace(request.AdminNotes))
                throw AppException.BadRequest("Rejection reason is required.", "REJECT_REASON_REQUIRED");

            await _subscriptionRepository.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;

                if (type == PackageType.Booth)
                {
                    var sub = await _subscriptionRepository.GetBoothSubscriptionByIdAsync(subscriptionId);
                    if (sub == null)
                        throw AppException.NotFound("Subscription was not found.");
                    if (sub.Status != SubscriptionStatus.PendingPayment)
                        throw AppException.Conflict("Subscription has already been processed.", "SUBSCRIPTION_ALREADY_PROCESSED");

                    var package = await _subscriptionRepository.GetPackageByIdAsync(sub.PackageId);
                    if (package == null || package.Status != PackageStatus.Active)
                        throw AppException.BadRequest("Package is no longer available.");
                    if (package.Type != PackageType.Booth)
                        throw AppException.BadRequest("Package type mismatch.");

                    if (request.IsApproved)
                    {
                        var existing = await _subscriptionRepository.GetLatestApprovedBoothSubscriptionAsync(sub.BoothId);
                        var durationDays = (sub.EndDate - sub.StartDate).Days;
                        if (durationDays <= 0) durationDays = package.DurationDays;

                        var startDate = now;
                        if (existing is not null && existing.EndDate > now)
                        {
                            startDate = existing.EndDate; // Stack on top of the existing active subscription
                        }

                        var endDate = startDate.AddDays(durationDays);

                        var rowsAffected = await _subscriptionRepository.UpdateBoothSubscriptionStatusAsync(
                            subscriptionId, SubscriptionStatus.PendingPayment, SubscriptionStatus.Active,
                            startDate, endDate, request.AdminNotes);

                        if (rowsAffected == 0)
                            throw AppException.Conflict("Subscription has already been processed by another administrator.", "SUBSCRIPTION_ALREADY_PROCESSED");
                    }
                    else
                    {
                        var rowsAffected = await _subscriptionRepository.UpdateBoothSubscriptionStatusAsync(
                            subscriptionId, SubscriptionStatus.PendingPayment, SubscriptionStatus.Cancelled,
                            sub.StartDate, sub.EndDate, request.AdminNotes);

                        if (rowsAffected == 0)
                            throw AppException.Conflict("Subscription has already been processed by another administrator.", "SUBSCRIPTION_ALREADY_PROCESSED");
                    }
                }
                else
                {
                    var sub = await _subscriptionRepository.GetMarketSubscriptionByIdAsync(subscriptionId);
                    if (sub == null)
                        throw AppException.NotFound("Subscription was not found.");
                    if (sub.Status != SubscriptionStatus.PendingPayment)
                        throw AppException.Conflict("Subscription has already been processed.", "SUBSCRIPTION_ALREADY_PROCESSED");

                    var package = await _subscriptionRepository.GetPackageByIdAsync(sub.PackageId);
                    if (package == null || package.Status != PackageStatus.Active)
                        throw AppException.BadRequest("Package is no longer available.");
                    if (package.Type != PackageType.Market)
                        throw AppException.BadRequest("Package type mismatch.");

                    if (request.IsApproved)
                    {
                        var existing = await _subscriptionRepository.GetLatestApprovedMarketSubscriptionAsync(sub.MarketOwnerId);
                        var durationDays = (sub.EndDate - sub.StartDate).Days;
                        if (durationDays <= 0) durationDays = package.DurationDays;

                        var startDate = now;
                        if (existing is not null && existing.EndDate > now)
                        {
                            startDate = existing.EndDate; // Stack on top of the existing active subscription
                        }

                        var endDate = startDate.AddDays(durationDays);

                        var rowsAffected = await _subscriptionRepository.UpdateMarketSubscriptionStatusAsync(
                            subscriptionId, SubscriptionStatus.PendingPayment, SubscriptionStatus.Active,
                            startDate, endDate, request.AdminNotes);

                        if (rowsAffected == 0)
                            throw AppException.Conflict("Subscription has already been processed by another administrator.", "SUBSCRIPTION_ALREADY_PROCESSED");
                    }
                    else
                    {
                        var rowsAffected = await _subscriptionRepository.UpdateMarketSubscriptionStatusAsync(
                            subscriptionId, SubscriptionStatus.PendingPayment, SubscriptionStatus.Cancelled,
                            sub.StartDate, sub.EndDate, request.AdminNotes);

                        if (rowsAffected == 0)
                            throw AppException.Conflict("Subscription has already been processed by another administrator.", "SUBSCRIPTION_ALREADY_PROCESSED");
                    }
                }

                await _subscriptionRepository.CommitTransactionAsync();

                var ownerType = type == PackageType.Booth ? "Booth" : "Market";
                object? ownerLog = type == PackageType.Booth
                    ? (object?)await _subscriptionRepository.GetBoothSubscriptionByIdAsync(subscriptionId)
                    : await _subscriptionRepository.GetMarketSubscriptionByIdAsync(subscriptionId);

                Guid? notifyUserId = null;
                string? packageName = null;
                if (ownerLog is BoothSubscription bs)
                {
                    notifyUserId = bs.Booth?.BoothOwnerId;
                    packageName = bs.Package?.PackageName;
                }
                else if (ownerLog is MarketSubscription ms)
                {
                    notifyUserId = ms.MarketOwnerId;
                    packageName = ms.Package?.PackageName;
                }

                if (notifyUserId.HasValue)
                {
                    var notifType = request.IsApproved
                        ? NotificationType.SubscriptionActivated
                        : NotificationType.SubscriptionRejected;
                    var title = request.IsApproved ? "Subscription Approved" : "Subscription Rejected";
                    var content = request.IsApproved
                        ? $"Your {ownerType} subscription for \"{packageName}\" has been approved and activated."
                        : $"Your {ownerType} subscription for \"{packageName}\" has been rejected. {(string.IsNullOrWhiteSpace(request.AdminNotes) ? "" : $"Reason: {request.AdminNotes}")}";

                    try
                    {
                        await _notifications.NotifyAsync(new NotificationMessage(
                            notifyUserId.Value,
                            notifType,
                            title,
                            content,
                            null,
                            "Subscription",
                            subscriptionId,
                            JsonSerializer.Serialize(new { subscriptionId, packageType = ownerType, approved = request.IsApproved })));
                    }
                    catch { /* notification failure should not block subscription processing */ }
                }

                return true;
            }
            catch (AppException)
            {
                await _subscriptionRepository.RollbackTransactionAsync();
                throw;
            }
            catch (Exception)
            {
                await _subscriptionRepository.RollbackTransactionAsync();
                throw;
            }
        }
    }
}
