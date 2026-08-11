using ApplicationLayer.DTOs;
using ApplicationLayer.DTOs.Subscriptions;
using ApplicationLayer.Configuration;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.PayOS;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Subscriptions
{
    public class OwnerSubscriptionService : IOwnerSubscriptionService
    {
        private const string BoothFreePackageCode = "BOOTH_FREE";

        private readonly ISubscriptionRepository _repo;
        private readonly IPayOSService _payos;
        private readonly INotificationService _notifications;
        private readonly IBoothRepository _boothRepo;
        private readonly IPayOSOrderCodeGenerator _orderCodeGenerator;
        private readonly PayOSSettings _payOSSettings;

        public OwnerSubscriptionService(
            ISubscriptionRepository repo,
            [FromKeyedServices("SubscriptionPayOS")] IPayOSService payos,
            INotificationService notifications,
            IBoothRepository boothRepo,
            IPayOSOrderCodeGenerator orderCodeGenerator,
            IOptions<PayOSSettings> payOSOptions)
        {
            _repo = repo;
            _payos = payos;
            _notifications = notifications;
            _boothRepo = boothRepo;
            _orderCodeGenerator = orderCodeGenerator;
            _payOSSettings = payOSOptions.Value;
        }

        private async Task EnsureBoothOwnershipAsync(Guid ownerId, Guid boothId, CancellationToken ct)
        {
            var booth = await _boothRepo.GetOwnedBoothAsync(ownerId, boothId);
            if (booth == null)
                throw AppException.Forbidden("You do not have permission to manage this booth.", "BOOTH_OWNERSHIP_REQUIRED");
        }

        // ─── Booth ───

        public async Task<ApiResponse<CurrentSubscriptionResponse>> GetBoothCurrentAsync(Guid ownerId, Guid boothId, CancellationToken ct = default)
        {
            await EnsureBoothOwnershipAsync(ownerId, boothId, ct);
            var sub = await _repo.GetActiveBoothSubscriptionAsync(boothId, ct);
            var pending = await _repo.GetPendingBoothSubscriptionAsync(boothId, ct);
            var hasPending = pending != null || await _repo.HasPendingBoothSubscriptionAsync(boothId, ct);

            if (sub == null)
            {
                // Fallback to BOOTH_FREE
                var freePackage = await _repo.GetPackageByCodeAsync(BoothFreePackageCode, ct);
                return ApiResponse<CurrentSubscriptionResponse>.SuccessResponse(new CurrentSubscriptionResponse
                {
                    Status = "Active",
                    PackageCode = "BOOTH_FREE",
                    PackageName = "Booth Basic",
                    PackageImageUrl = freePackage?.ImageUrl,
                    DaysRemaining = 0,
                    Entitlements = EntitlementHelper.SerializeBooth(BoothEntitlements.Free),
                    PaidAmount = 0,
                    HasPendingRequest = hasPending,
                    PendingSubscriptionId = pending?.Id,
                    PendingPackageCode = pending?.Package?.Code,
                    PendingPackageName = pending?.Package?.PackageName,
                    PendingStatus = pending?.Status.ToString(),
                    PendingExpiresAt = pending?.PaymentExpiresAt,
                    PendingPaymentExpiresAt = pending?.PaymentExpiresAt,
                });
            }

            return ApiResponse<CurrentSubscriptionResponse>.SuccessResponse(MapBoothCurrent(sub, hasPending, pending));
        }

        public async Task<ApiResponse<List<SubscriptionHistoryItem>>> GetBoothHistoryAsync(Guid ownerId, Guid boothId, CancellationToken ct = default)
        {
            await EnsureBoothOwnershipAsync(ownerId, boothId, ct);
            var list = await _repo.GetBoothSubscriptionHistoryAsync(boothId, ct);
            return ApiResponse<List<SubscriptionHistoryItem>>.SuccessResponse(list.Select(MapHistory).ToList());
        }

        public async Task<ApiResponse<PayOSPaymentResponseDto>> PurchaseBoothAsync(Guid ownerId, Guid boothId, PurchaseSubscriptionRequest request, CancellationToken ct = default)
        {
            await EnsureBoothOwnershipAsync(ownerId, boothId, ct);
            var (pkg, policy, price, durationDays, amount) = await ResolvePackageAndPriceAsync(request.PackageId, request.DurationDays, PackageType.Booth, ct);

            var pending = await _repo.GetPendingBoothSubscriptionAsync(boothId, ct);
            if (pending != null && pending.PackageId == pkg.Id)
            {
                var resumed = await HandlePendingBoothPaymentAsync(pending, pkg, durationDays, ct);
                if (resumed != null)
                    return resumed;
            }

            // A pending payment for a different package is cancelled before the new
            // selection starts. The same-package path above resumes rather than
            // returning a generic conflict.
            await AutoCancelStalePendingBoothPaymentAsync(boothId, pkg.Id, ct);

            var active = await _repo.GetActiveBoothSubscriptionAsync(boothId, ct);
            if (IsBoothFreePackage(pkg))
            {
                return await ActivateFreeBoothSubscriptionAsync(boothId, pkg, durationDays, active, ct);
            }

            if (amount <= 0)
                throw AppException.BadRequest("Package price must be greater than zero.", "INVALID_PACKAGE_PRICE");

            var policyAcceptance = ValidateAndSnapshotPolicy(policy, request.AcceptedPolicy, request.AcceptedPolicyVersion);

            if (active?.PackageId == pkg.Id)
                throw AppException.Conflict("This package is already active. Use renewal to extend it.", "USE_RENEWAL_ENDPOINT");

            var now = DateTime.UtcNow;
            var changeType = DetermineChangeType(active?.Package, pkg);
            var creditAmount = changeType == "Upgrade" ? CalculateProratedCredit(active, now) : 0m;
            var amountDue = Math.Max(0m, amount - creditAmount);
            var subscription = new BoothSubscription
            {
                BoothId = boothId,
                PackageId = pkg.Id,
                StartDate = now,
                EndDate = now.AddDays(durationDays),
                Status = SubscriptionStatus.PendingPayment,
                PaidAmount = amountDue,
                PolicyVersion = policyAcceptance.Version,
                PolicyAcceptedAt = policyAcceptance.AcceptedAt,
                PolicySnapshotJson = policyAcceptance.SnapshotJson,
                ChangeType = changeType,
                PreviousSubscriptionId = active?.Id,
                CreditAmount = creditAmount,
            };
            await _repo.AddBoothSubscriptionAsync(subscription, ct);
            await _repo.SaveChangesAsync(ct);

            if (amountDue == 0)
                return await ActivateCreditCoveredBoothSubscriptionAsync(subscription, pkg, durationDays, active, now, ct);

            var orderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.BoothSubscription);
            try
            {
                var payosResp = await _payos.CreatePaymentLinkAsync(
                    BuildSubscriptionPaymentRequest(orderCode, amountDue, subscription.Id, "booth"));

                subscription.PayOSOrderCode = orderCode;
                subscription.PayOSPaymentLinkId = payosResp.PaymentLinkId;
                subscription.PaymentExpiresAt = ParsePayOSExpiry(payosResp.ExpiresAt);
                await _repo.SaveChangesAsync(ct);

                return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(MapPayOSResponse(subscription.Id, pkg.PackageName, durationDays, amountDue, payosResp));
            }
            catch
            {
                await _repo.CancelBoothSubscriptionAsync(subscription.Id, ct);
                await _repo.SaveChangesAsync(ct);
                throw;
            }
        }

        public async Task<ApiResponse<PayOSPaymentResponseDto>> RenewBoothAsync(Guid ownerId, Guid boothId, RenewSubscriptionRequest request, CancellationToken ct = default)
        {
            await EnsureBoothOwnershipAsync(ownerId, boothId, ct);
            var current = await _repo.GetActiveBoothSubscriptionAsync(boothId, ct)
                ?? throw AppException.BadRequest("No active subscription to renew.", "NO_ACTIVE_SUBSCRIPTION");

            var durationDays = request.DurationDays ?? current.Package.DurationDays;
            var (pkg, policy, price, resolvedDuration, amount) = await ResolvePackageAndPriceAsync(current.PackageId, durationDays, PackageType.Booth, ct);

            if (IsBoothFreePackage(pkg))
                return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(
                    MapDirectActivationResponse(current.Id, pkg.PackageName, resolvedDuration, 0));

            var pending = await _repo.GetPendingBoothSubscriptionAsync(boothId, ct);
            if (pending != null && pending.PackageId == pkg.Id)
            {
                var resumed = await HandlePendingBoothPaymentAsync(pending, pkg, resolvedDuration, ct);
                if (resumed != null)
                    return resumed;
            }

            var policyAcceptance = ValidateAndSnapshotPolicy(policy, request.AcceptedPolicy, request.AcceptedPolicyVersion);

            if (amount <= 0)
                throw AppException.BadRequest("Package price must be greater than zero.", "INVALID_PACKAGE_PRICE");

            // A different pending package is closed before a new renewal begins.
            // The matching-package path above resumes its existing payment link.
            await AutoCancelStalePendingBoothPaymentAsync(boothId, pkg.Id, ct);

            var now = DateTime.UtcNow;
            var subscription = new BoothSubscription
            {
                BoothId = boothId,
                PackageId = pkg.Id,
                StartDate = now,
                EndDate = now.AddDays(resolvedDuration),
                Status = SubscriptionStatus.PendingPayment,
                PaidAmount = amount,
                PolicyVersion = policyAcceptance.Version,
                PolicyAcceptedAt = policyAcceptance.AcceptedAt,
                PolicySnapshotJson = policyAcceptance.SnapshotJson,
                ChangeType = "Renewal",
                PreviousSubscriptionId = current.Id,
            };
            await _repo.AddBoothSubscriptionAsync(subscription, ct);
            await _repo.SaveChangesAsync(ct);

            var orderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.BoothSubscription);
            try
            {
                var payosResp = await _payos.CreatePaymentLinkAsync(
                    BuildSubscriptionPaymentRequest(orderCode, amount, subscription.Id, "booth"));

                subscription.PayOSOrderCode = orderCode;
                subscription.PayOSPaymentLinkId = payosResp.PaymentLinkId;
                subscription.PaymentExpiresAt = ParsePayOSExpiry(payosResp.ExpiresAt);
                await _repo.SaveChangesAsync(ct);

                return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(MapPayOSResponse(subscription.Id, pkg.PackageName, resolvedDuration, amount, payosResp));
            }
            catch
            {
                await _repo.CancelBoothSubscriptionAsync(subscription.Id, ct);
                await _repo.SaveChangesAsync(ct);
                throw;
            }
        }

        // ─── Market ───

        public async Task<ApiResponse<CurrentSubscriptionResponse>> GetMarketCurrentAsync(Guid ownerId, CancellationToken ct = default)
        {
            var sub = await _repo.GetActiveMarketSubscriptionAsync(ownerId, ct);
            var pending = await _repo.GetPendingMarketSubscriptionAsync(ownerId, ct);

            // A pending purchase is still the user's current subscription state even
            // when there is no active plan yet. Returning None here made the FE lose
            // the package/order and caused repeated 404/409 attempts on retry.
            if (sub == null && pending != null)
                return ApiResponse<CurrentSubscriptionResponse>.SuccessResponse(MapMarketCurrent(pending, hasPending: true, pendingInfo: pending));

            if (sub == null)
                return ApiResponse<CurrentSubscriptionResponse>.SuccessResponse(new CurrentSubscriptionResponse { Status = "None" });

            return ApiResponse<CurrentSubscriptionResponse>.SuccessResponse(MapMarketCurrent(sub, pending != null, pendingInfo: pending));
        }

        public async Task<ApiResponse<List<SubscriptionHistoryItem>>> GetMarketHistoryAsync(Guid ownerId, CancellationToken ct = default)
        {
            var list = await _repo.GetMarketSubscriptionHistoryAsync(ownerId, ct);
            return ApiResponse<List<SubscriptionHistoryItem>>.SuccessResponse(list.Select(MapHistory).ToList());
        }

        public async Task<ApiResponse<PayOSPaymentResponseDto>> PurchaseMarketAsync(Guid ownerId, PurchaseSubscriptionRequest request, CancellationToken ct = default)
        {
            // Resume an existing pending payment before resolving a newly selected
            // package. A stopped/updated package must not turn a valid pending QR
            // into a misleading 404, and a different package must be rejected
            // explicitly as a pending-payment conflict.
            var pending = await _repo.GetPendingMarketSubscriptionAsync(ownerId, ct);
            if (pending != null)
            {
                if (pending.PackageId == request.PackageId)
                {
                    var pendingPackage = pending.Package
                        ?? throw AppException.NotFound(
                            "The package for your pending payment is no longer available. Please contact Support.",
                            "PACKAGE_NOT_FOUND");
                    var pendingDuration = request.DurationDays ?? pendingPackage.DurationDays;
                    var resumed = await HandlePendingMarketPaymentAsync(pending, pendingPackage, pendingDuration, ct);
                    if (resumed != null)
                        return resumed;
                }
                else
                {
                    // Different package - auto-cancel stale pending and proceed with new purchase
                    await AutoCancelStalePendingMarketPaymentAsync(ownerId, request.PackageId, ct);
                }
            }

            var (pkg, policy, price, durationDays, amount) = await ResolvePackageAndPriceAsync(request.PackageId, request.DurationDays, PackageType.Market, ct);

            if (amount <= 0)
                throw AppException.BadRequest("Market package price must be greater than 0.", "INVALID_PACKAGE_PRICE");

            // Now validate policy for new purchase
            var policyAcceptance = ValidateAndSnapshotPolicy(policy, request.AcceptedPolicy, request.AcceptedPolicyVersion);

            var active = await _repo.GetActiveMarketSubscriptionAsync(ownerId, ct);
            if (active?.PackageId == pkg.Id)
                throw AppException.Conflict("This package is already active. Use renewal to extend it.", "USE_RENEWAL_ENDPOINT");

            var now2 = DateTime.UtcNow;
            var changeType = DetermineChangeType(active?.Package, pkg);
            var creditAmount = changeType == "Upgrade" ? CalculateProratedCredit(active, now2) : 0m;
            var amountDue = Math.Max(0m, amount - creditAmount);
            var subscription = new MarketSubscription
            {
                MarketOwnerId = ownerId,
                PackageId = pkg.Id,
                StartDate = now2,
                EndDate = now2.AddDays(durationDays),
                Status = SubscriptionStatus.PendingPayment,
                PaidAmount = amountDue,
                PolicyVersion = policyAcceptance.Version,
                PolicyAcceptedAt = policyAcceptance.AcceptedAt,
                PolicySnapshotJson = policyAcceptance.SnapshotJson,
                ChangeType = changeType,
                PreviousSubscriptionId = active?.Id,
                CreditAmount = creditAmount,
            };
            await _repo.AddMarketSubscriptionAsync(subscription, ct);
            await _repo.SaveChangesAsync(ct);

            if (amountDue == 0)
                return await ActivateCreditCoveredMarketSubscriptionAsync(subscription, pkg, durationDays, active, now2, ct);

            var newOrderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.MarketSubscription);
            try
            {
                var payosResp = await _payos.CreatePaymentLinkAsync(
                    BuildSubscriptionPaymentRequest(newOrderCode, amountDue, subscription.Id, "market"));

                subscription.PayOSOrderCode = newOrderCode;
                subscription.PayOSPaymentLinkId = payosResp.PaymentLinkId;
                subscription.PaymentExpiresAt = ParsePayOSExpiry(payosResp.ExpiresAt);
                await _repo.SaveChangesAsync(ct);

                return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(MapPayOSResponse(subscription.Id, pkg.PackageName, durationDays, amountDue, payosResp));
            }
            catch
            {
                await _repo.CancelMarketSubscriptionAsync(subscription.Id, ct);
                await _repo.SaveChangesAsync(ct);
                throw;
            }
        }
        public async Task<ApiResponse<PayOSPaymentResponseDto>> RenewMarketAsync(Guid ownerId, RenewSubscriptionRequest request, CancellationToken ct = default)
        {
            var current = await _repo.GetActiveMarketSubscriptionAsync(ownerId, ct)
                ?? throw AppException.BadRequest("No active subscription to renew.", "NO_ACTIVE_SUBSCRIPTION");

            var durationDays = request.DurationDays ?? current.Package.DurationDays;
            var (pkg, policy, price, resolvedDuration, amount) = await ResolvePackageAndPriceAsync(current.PackageId, durationDays, PackageType.Market, ct);

            if (amount <= 0)
                throw AppException.BadRequest("Market package price must be greater than 0.", "INVALID_PACKAGE_PRICE");

            // Check pending payment BEFORE policy validation
            var pending = await _repo.GetPendingMarketSubscriptionAsync(ownerId, ct);
            if (pending != null)
            {
                // Same package - try to resume pending payment
                if (pending.PackageId == pkg.Id)
                {
                    var pendingResult = await HandlePendingMarketPaymentAsync(pending, pkg, resolvedDuration, ct);
                    if (pendingResult != null)
                        return pendingResult;
                }
                else
                {
                    // Different package - auto-cancel stale pending and proceed
                    await AutoCancelStalePendingMarketPaymentAsync(ownerId, pkg.Id, ct);
                }
            }

            // Now validate policy for new renewal
            var policyAcceptance = ValidateAndSnapshotPolicy(policy, request.AcceptedPolicy, request.AcceptedPolicyVersion);

            var now = DateTime.UtcNow;
            var subscription = new MarketSubscription
            {
                MarketOwnerId = ownerId,
                PackageId = pkg.Id,
                StartDate = now,
                EndDate = now.AddDays(resolvedDuration),
                Status = SubscriptionStatus.PendingPayment,
                PaidAmount = amount,
                PolicyVersion = policyAcceptance.Version,
                PolicyAcceptedAt = policyAcceptance.AcceptedAt,
                PolicySnapshotJson = policyAcceptance.SnapshotJson,
                ChangeType = "Renewal",
                PreviousSubscriptionId = current.Id,
            };
            await _repo.AddMarketSubscriptionAsync(subscription, ct);
            await _repo.SaveChangesAsync(ct);

            var orderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.MarketSubscription);
            try
            {
                var payosResp = await _payos.CreatePaymentLinkAsync(
                    BuildSubscriptionPaymentRequest(orderCode, amount, subscription.Id, "market"));

                subscription.PayOSOrderCode = orderCode;
                subscription.PayOSPaymentLinkId = payosResp.PaymentLinkId;
                subscription.PaymentExpiresAt = ParsePayOSExpiry(payosResp.ExpiresAt);
                await _repo.SaveChangesAsync(ct);

                return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(MapPayOSResponse(subscription.Id, pkg.PackageName, resolvedDuration, amount, payosResp));
            }
            catch
            {
                await _repo.CancelMarketSubscriptionAsync(subscription.Id, ct);
                await _repo.SaveChangesAsync(ct);
                throw;
            }
        }

        // ─── Payment Status & Cancel ───

        public async Task<ApiResponse<PaymentStatusResponse>> GetPaymentStatusAsync(Guid userId, Guid subscriptionId, CancellationToken ct = default)
        {
            var boothSub = await _repo.GetBoothSubscriptionByIdAsync(subscriptionId, ct);
            if (boothSub != null)
            {
                if (boothSub.Booth?.BoothOwnerId != userId)
                    throw AppException.Forbidden("You do not own this subscription.");
                return ApiResponse<PaymentStatusResponse>.SuccessResponse(new PaymentStatusResponse
                {
                    SubscriptionId = boothSub.Id,
                    Status = boothSub.Status.ToString(),
                    PaidAmount = boothSub.PaidAmount,
                    PaidAt = boothSub.PaidAt,
                    StartDate = boothSub.StartDate,
                    EndDate = boothSub.EndDate,
                });
            }

            var marketSub = await _repo.GetMarketSubscriptionByIdAsync(subscriptionId, ct);
            if (marketSub != null)
            {
                if (marketSub.MarketOwnerId != userId)
                    throw AppException.Forbidden("You do not own this subscription.");
                return ApiResponse<PaymentStatusResponse>.SuccessResponse(new PaymentStatusResponse
                {
                    SubscriptionId = marketSub.Id,
                    Status = marketSub.Status.ToString(),
                    PaidAmount = marketSub.PaidAmount,
                    PaidAt = marketSub.PaidAt,
                    StartDate = marketSub.StartDate,
                    EndDate = marketSub.EndDate,
                });
            }

            throw AppException.NotFound("Subscription was not found.");
        }

        public async Task<ApiResponse<CancelPaymentResponse>> CancelPaymentAsync(Guid userId, Guid subscriptionId, CancellationToken ct = default)
        {
            var boothSub = await _repo.GetBoothSubscriptionByIdAsync(subscriptionId, ct);
            if (boothSub != null)
            {
                if (boothSub.Booth?.BoothOwnerId != userId)
                    throw AppException.Forbidden("You do not own this subscription.");
                if (boothSub.Status != SubscriptionStatus.PendingPayment)
                    throw AppException.Conflict("Only pending payments can be cancelled.", "SUBSCRIPTION_NOT_PENDING");

                // Cancel PayOS payment link first
                if (boothSub.PayOSOrderCode.HasValue && boothSub.PayOSOrderCode.Value > 0)
                    await _payos.CancelPaymentLinkAsync(boothSub.PayOSOrderCode.Value);

                await _repo.CancelBoothSubscriptionAsync(subscriptionId, ct);
                await _repo.SaveChangesAsync(ct);
                return ApiResponse<CancelPaymentResponse>.SuccessResponse(new CancelPaymentResponse
                {
                    SubscriptionId = subscriptionId,
                    Status = "Cancelled",
                    Message = "Payment has been cancelled.",
                });
            }

            var marketSub = await _repo.GetMarketSubscriptionByIdAsync(subscriptionId, ct);
            if (marketSub != null)
            {
                if (marketSub.MarketOwnerId != userId)
                    throw AppException.Forbidden("You do not own this subscription.");
                if (marketSub.Status != SubscriptionStatus.PendingPayment)
                    throw AppException.Conflict("Only pending payments can be cancelled.", "SUBSCRIPTION_NOT_PENDING");

                // Cancel PayOS payment link first
                if (marketSub.PayOSOrderCode.HasValue && marketSub.PayOSOrderCode.Value > 0)
                    await _payos.CancelPaymentLinkAsync(marketSub.PayOSOrderCode.Value);

                await _repo.CancelMarketSubscriptionAsync(subscriptionId, ct);
                await _repo.SaveChangesAsync(ct);
                return ApiResponse<CancelPaymentResponse>.SuccessResponse(new CancelPaymentResponse
                {
                    SubscriptionId = subscriptionId,
                    Status = "Cancelled",
                    Message = "Payment has been cancelled.",
                });
            }

            throw AppException.NotFound("Subscription was not found.");
        }

        // ─── Helpers ───

        private async Task<(Package pkg, PackagePolicy? policy, PackagePrice? price, int durationDays, decimal amount)> ResolvePackageAndPriceAsync(Guid packageId, int? durationDays, PackageType expectedType, CancellationToken ct)
        {
            var pkg = await _repo.GetPackageByIdAsync(packageId, ct)
                ?? throw AppException.NotFound("The selected package is no longer available. Please refresh the available plans and choose an active package.", "PACKAGE_NOT_FOUND");

            if (pkg.IsDeleted || pkg.Status != PackageStatus.Active)
                throw AppException.BadRequest("Package is no longer available.", "PACKAGE_NOT_AVAILABLE");
            if (pkg.Type != expectedType)
                throw AppException.BadRequest("Package type mismatch.");

            var policy = IsBoothFreePackage(pkg)
                ? null
                : await _repo.GetActivePackagePolicyAsync(packageId, ct);

            var resolvedDuration = durationDays ?? pkg.DurationDays;
            var prices = await _repo.GetActivePricesForPackageAsync(packageId, ct);
            var now = DateTime.UtcNow;
            // Filter prices by validity period (StartDate/EndDate) to avoid expired promotions
            var validPrice = prices
                .Where(p => p.DurationDays == resolvedDuration)
                .Where(p => (!p.StartDate.HasValue || p.StartDate.Value <= now)
                            && (!p.EndDate.HasValue || p.EndDate.Value >= now))
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();
            var price = validPrice;

            decimal amount;
            if (price != null)
            {
                amount = price.Price;
            }
            else
            {
                amount = pkg.Price;
                resolvedDuration = pkg.DurationDays;
            }

            return (pkg, policy, price, resolvedDuration, amount);
        }

        private async Task<ApiResponse<PayOSPaymentResponseDto>> ActivateFreeBoothSubscriptionAsync(
            Guid boothId,
            Package package,
            int durationDays,
            BoothSubscription? active,
            CancellationToken ct)
        {
            if (active is not null)
            {
                if (IsBoothFreePackage(active.Package))
                {
                    return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(
                        MapDirectActivationResponse(active.Id, package.PackageName, durationDays, 0));
                }

                throw AppException.Conflict(
                    "Your paid booth subscription is still active. It remains in effect until its end date.",
                    "PAID_SUBSCRIPTION_ACTIVE");
            }

            var now = DateTime.UtcNow;
            var subscription = new BoothSubscription
            {
                BoothId = boothId,
                PackageId = package.Id,
                StartDate = now,
                EndDate = DateTime.MaxValue,
                Status = SubscriptionStatus.PendingPayment,
                PaidAmount = 0,
                ChangeType = "FreeDefault",
                CreatedAt = now,
                UpdatedAt = now
            };

            await _repo.AddBoothSubscriptionAsync(subscription, ct);
            await _repo.SaveChangesAsync(ct);

            var rowsAffected = await _repo.ActivateBoothSubscriptionAsync(subscription.Id, now, subscription.EndDate, now, ct);
            if (rowsAffected == 0)
                throw AppException.Conflict("The free booth subscription could not be activated. Please try again.", "FREE_SUBSCRIPTION_ACTIVATION_FAILED");

            await _repo.SaveChangesAsync(ct);
            return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(
                MapDirectActivationResponse(subscription.Id, package.PackageName, durationDays, 0));
        }

        private async Task<ApiResponse<PayOSPaymentResponseDto>> ActivateCreditCoveredBoothSubscriptionAsync(
            BoothSubscription subscription,
            Package package,
            int durationDays,
            BoothSubscription? previousSubscription,
            DateTime now,
            CancellationToken ct)
        {
            await _repo.BeginTransactionAsync(ct);
            try
            {
                if (subscription.ChangeType == "Upgrade" && previousSubscription is not null)
                {
                    var expiredRows = await _repo.UpdateBoothSubscriptionStatusAsync(
                        previousSubscription.Id,
                        SubscriptionStatus.Active,
                        SubscriptionStatus.Expired,
                        previousSubscription.StartDate,
                        now,
                        "Replaced by an upgrade.",
                        ct);
                    if (expiredRows != 1)
                        throw AppException.Conflict("The current subscription changed before the upgrade could be applied.", "SUBSCRIPTION_CHANGE_CONFLICT");
                }

                var activatedRows = await _repo.ActivateBoothSubscriptionAsync(
                    subscription.Id,
                    now,
                    now.AddDays(durationDays),
                    now,
                    ct);
                if (activatedRows != 1)
                    throw AppException.Conflict("The subscription could not be activated. Please try again.", "SUBSCRIPTION_ACTIVATION_CONFLICT");

                await _repo.SaveChangesAsync(ct);
                await _repo.CommitTransactionAsync(ct);
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                await _repo.CancelBoothSubscriptionAsync(subscription.Id, ct);
                await _repo.SaveChangesAsync(ct);
                throw;
            }

            return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(
                MapDirectActivationResponse(subscription.Id, package.PackageName, durationDays, 0));
        }

        private async Task<ApiResponse<PayOSPaymentResponseDto>> ActivateCreditCoveredMarketSubscriptionAsync(
            MarketSubscription subscription,
            Package package,
            int durationDays,
            MarketSubscription? previousSubscription,
            DateTime now,
            CancellationToken ct)
        {
            await _repo.BeginTransactionAsync(ct);
            try
            {
                if (subscription.ChangeType == "Upgrade" && previousSubscription is not null)
                {
                    var expiredRows = await _repo.UpdateMarketSubscriptionStatusAsync(
                        previousSubscription.Id,
                        SubscriptionStatus.Active,
                        SubscriptionStatus.Expired,
                        previousSubscription.StartDate,
                        now,
                        "Replaced by an upgrade.",
                        ct);
                    if (expiredRows != 1)
                        throw AppException.Conflict("The current subscription changed before the upgrade could be applied.", "SUBSCRIPTION_CHANGE_CONFLICT");
                }

                var activatedRows = await _repo.ActivateMarketSubscriptionAsync(
                    subscription.Id,
                    now,
                    now.AddDays(durationDays),
                    now,
                    ct);
                if (activatedRows != 1)
                    throw AppException.Conflict("The subscription could not be activated. Please try again.", "SUBSCRIPTION_ACTIVATION_CONFLICT");

                await _repo.SaveChangesAsync(ct);
                await _repo.CommitTransactionAsync(ct);
            }
            catch
            {
                await _repo.RollbackTransactionAsync(ct);
                await _repo.CancelMarketSubscriptionAsync(subscription.Id, ct);
                await _repo.SaveChangesAsync(ct);
                throw;
            }

            return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(
                MapDirectActivationResponse(subscription.Id, package.PackageName, durationDays, 0));
        }

        private static bool IsBoothFreePackage(Package package)
            => package.Type == PackageType.Booth
                && string.Equals(package.Code, BoothFreePackageCode, StringComparison.OrdinalIgnoreCase);

        private static (string Version, DateTime AcceptedAt, string SnapshotJson) ValidateAndSnapshotPolicy(
            PackagePolicy? policy,
            bool acceptedPolicy,
            string? acceptedPolicyVersion)
        {
            if (policy is null)
                throw AppException.Conflict("This package does not have an active policy available for acceptance.", "POLICY_NOT_AVAILABLE");

            if (!acceptedPolicy)
                throw AppException.BadRequest("You must accept the package policy before continuing.", "POLICY_ACCEPTANCE_REQUIRED");

            if (!string.Equals(policy.Version, acceptedPolicyVersion, StringComparison.Ordinal))
                throw AppException.Conflict("The package policy has changed. Review and accept the current version before continuing.", "POLICY_VERSION_MISMATCH");

            var acceptedAt = DateTime.UtcNow;
            var snapshotJson = JsonSerializer.Serialize(new
            {
                policy.Version,
                policy.Title,
                policy.ContentJson,
                policy.ContentMarkdown,
                policy.EffectiveFrom
            });

            return (policy.Version, acceptedAt, snapshotJson);
        }

        private async Task AutoCancelStalePendingBoothPaymentAsync(Guid boothId, Guid targetPackageId, CancellationToken ct)
        {
            var pending = await _repo.GetPendingBoothSubscriptionAsync(boothId, ct);
            if (pending == null)
                return;

            // Same package and not expired - let caller handle resume logic
            if (pending.PackageId == targetPackageId)
            {
                var isExpired = pending.PaymentExpiresAt.HasValue && pending.PaymentExpiresAt.Value <= DateTime.UtcNow;
                if (!isExpired)
                    throw AppException.Conflict("This booth already has a pending payment for the same package. Complete or cancel it before starting another change.", "PENDING_SUBSCRIPTION_EXISTS");
            }

            // Cancel PayOS link if exists
            if (pending.PayOSOrderCode.HasValue && pending.PayOSOrderCode.Value > 0)
            {
                try { await _payos.CancelPaymentLinkAsync(pending.PayOSOrderCode.Value); }
                catch { /* Ignore PayOS errors when cancelling stale pending */ }
            }

            await _repo.CancelBoothSubscriptionAsync(pending.Id, ct);
            await _repo.SaveChangesAsync(ct);
        }

        private async Task AutoCancelStalePendingMarketPaymentAsync(Guid ownerId, Guid targetPackageId, CancellationToken ct)
        {
            var pending = await _repo.GetPendingMarketSubscriptionAsync(ownerId, ct);
            if (pending == null)
                return;

            // Same package and not expired - let caller handle resume logic
            if (pending.PackageId == targetPackageId)
            {
                var isExpired = pending.PaymentExpiresAt.HasValue && pending.PaymentExpiresAt.Value <= DateTime.UtcNow;
                if (!isExpired)
                    return; // Let HandlePendingMarketPaymentAsync handle it
            }

            // Cancel PayOS link if exists
            if (pending.PayOSOrderCode.HasValue && pending.PayOSOrderCode.Value > 0)
            {
                try { await _payos.CancelPaymentLinkAsync(pending.PayOSOrderCode.Value); }
                catch { /* Ignore PayOS errors when cancelling stale pending */ }
            }

            await _repo.CancelMarketSubscriptionAsync(pending.Id, ct);
            await _repo.SaveChangesAsync(ct);
        }

        private static string DetermineChangeType(Package? activePackage, Package targetPackage)
        {
            if (activePackage is null)
                return "New";

            return GetPackageRank(targetPackage) > GetPackageRank(activePackage)
                ? "Upgrade"
                : "DowngradeScheduled";
        }

        private static int GetPackageRank(Package package)
        {
            return package.Code?.ToUpperInvariant() switch
            {
                "BOOTH_FREE" => 0,
                "BOOTH_GROWTH" => 1,
                "BOOTH_FEATURED" => 2,
                "MARKET_BASIC" => 1,
                "MARKET_PRO" => 2,
                _ => package.Price > 0 ? 1 : 0
            };
        }

        private static decimal CalculateProratedCredit(BoothSubscription? subscription, DateTime now)
            => subscription is null
                ? 0m
                : CalculateProratedCredit(subscription.StartDate, subscription.EndDate, subscription.PaidAmount, now);

        private static decimal CalculateProratedCredit(MarketSubscription? subscription, DateTime now)
            => subscription is null
                ? 0m
                : CalculateProratedCredit(subscription.StartDate, subscription.EndDate, subscription.PaidAmount, now);

        private static decimal CalculateProratedCredit(DateTime startDate, DateTime endDate, decimal paidAmount, DateTime now)
        {
            if (paidAmount <= 0 || endDate <= now)
                return 0m;

            var totalDays = (decimal)(endDate - startDate).TotalDays;
            if (totalDays <= 0)
                return 0m;

            var remainingDays = Math.Min((decimal)(endDate - now).TotalDays, totalDays);
            return Math.Round(paidAmount * remainingDays / totalDays, 0, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Reopens a Booth Owner's own pending payment for the same package.
        /// This mirrors the market-owner flow: retrying a payment is not a new
        /// purchase and must not be rejected as a subscription conflict.
        /// </summary>
        private async Task<ApiResponse<PayOSPaymentResponseDto>?> HandlePendingBoothPaymentAsync(
            BoothSubscription pending, Package targetPkg, int durationDays, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var isExpired = pending.PaymentExpiresAt.HasValue && pending.PaymentExpiresAt.Value <= now;

            if (!isExpired && pending.PackageId != targetPkg.Id)
                throw AppException.Conflict("You already have a pending payment for a different package. Complete or cancel it first.", "PENDING_PAYMENT_EXISTS");

            if (pending.PayOSOrderCode.HasValue)
            {
                try
                {
                    var paymentStatus = await _payos.GetPaymentStatusAsync(pending.PayOSOrderCode.Value);
                    if (paymentStatus != null)
                    {
                        if (string.Equals(paymentStatus.Status, "PAID", StringComparison.OrdinalIgnoreCase))
                        {
                            return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(
                                new PayOSPaymentResponseDto
                                {
                                    SubscriptionId = pending.Id,
                                    OrderCode = pending.PayOSOrderCode.Value,
                                    PackageName = targetPkg.PackageName,
                                    DurationDays = durationDays,
                                    Amount = pending.PaidAmount,
                                    Status = "AwaitingWebhook",
                                },
                                "Payment has been confirmed. Your subscription is being activated.");
                        }

                        if (!isExpired && string.Equals(paymentStatus.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                        {
                            return await RecreatePendingBoothPaymentLinkAsync(
                                pending,
                                targetPkg.PackageName,
                                durationDays,
                                "Your pending payment has been resumed. Please complete the payment.",
                                ct);
                        }
                    }
                }
                catch
                {
                    // The provider status is advisory here. A fresh link below is
                    // safer than leaving the owner without a way to continue.
                }
            }

            if (!isExpired && pending.PackageId == targetPkg.Id && pending.PayOSOrderCode.HasValue)
            {
                return await RecreatePendingBoothPaymentLinkAsync(
                    pending,
                    targetPkg.PackageName,
                    durationDays,
                    "A new payment link has been created. Please complete the payment.",
                    ct);
            }

            await _repo.CancelBoothSubscriptionAsync(pending.Id, ct);
            await _repo.SaveChangesAsync(ct);
            return null;
        }

        private async Task<ApiResponse<PayOSPaymentResponseDto>> RecreatePendingBoothPaymentLinkAsync(
            BoothSubscription pending,
            string packageName,
            int durationDays,
            string message,
            CancellationToken ct)
        {
            if (pending.PayOSOrderCode.HasValue)
            {
                try { await _payos.CancelPaymentLinkAsync(pending.PayOSOrderCode.Value); }
                catch { }
            }

            var orderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.BoothSubscription);
            var payment = await _payos.CreatePaymentLinkAsync(
                BuildSubscriptionPaymentRequest(orderCode, pending.PaidAmount, pending.Id, "booth"));

            pending.PayOSOrderCode = orderCode;
            pending.PayOSPaymentLinkId = payment.PaymentLinkId;
            pending.PaymentExpiresAt = ParsePayOSExpiry(payment.ExpiresAt);
            await _repo.SaveChangesAsync(ct);

            return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(
                MapPayOSResponse(pending.Id, packageName, durationDays, pending.PaidAmount, payment, "PendingPaymentResumed"),
                message);
        }

        /// <summary>
        /// Handles an existing pending market subscription payment.
        /// Returns a response if the pending payment is resolved (resumed/awaiting/paid),
        /// or null if the caller should proceed with a new purchase (pending was cancelled/expired).
        /// </summary>
        private async Task<ApiResponse<PayOSPaymentResponseDto>?> HandlePendingMarketPaymentAsync(
            MarketSubscription pending, Package targetPkg, int durationDays, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var isExpired = pending.PaymentExpiresAt.HasValue && pending.PaymentExpiresAt.Value <= now;

            // Different package and not expired -> block
            if (!isExpired && pending.PackageId != targetPkg.Id)
                throw AppException.Conflict("You already have a pending payment for a different package. Complete or cancel it first.", "PENDING_PAYMENT_EXISTS");

            // Check PayOS status of the existing order before doing anything
            if (pending.PayOSOrderCode.HasValue)
            {
                try
                {
                    var payosStatus = await _payos.GetPaymentStatusAsync(pending.PayOSOrderCode.Value);
                    if (payosStatus != null)
                    {
                        // Already paid - webhook may not have arrived yet. Don't create new QR.
                        if (string.Equals(payosStatus.Status, "PAID", StringComparison.OrdinalIgnoreCase))
                        {
                            return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(
                                new PayOSPaymentResponseDto
                                {
                                    SubscriptionId = pending.Id,
                                    OrderCode = pending.PayOSOrderCode.Value,
                                    PackageName = targetPkg.PackageName,
                                    DurationDays = durationDays,
                                    Amount = pending.PaidAmount,
                                    Status = "AwaitingWebhook",
                                },
                                "Payment has been confirmed. Your subscription is being activated.");
                        }

                        // Still pending and not expired. Subscription rows do not persist
                        // checkout URL/QR, so create a fresh payment link on the same subscription.
                        if (!isExpired && string.Equals(payosStatus.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                        {
                            return await RecreatePendingMarketPaymentLinkAsync(
                                pending,
                                targetPkg.PackageName,
                                durationDays,
                                "Your pending payment has been resumed. Please complete the payment.",
                                ct);
                        }
                    }
                }
                catch
                {
                    // If PayOS status check fails, fall through to expiry-based logic
                }
            }

            // Expired or cancelled on PayOS side - cancel old pending and create new
            if (!isExpired && pending.PackageId == targetPkg.Id && pending.PayOSOrderCode.HasValue)
            {
                return await RecreatePendingMarketPaymentLinkAsync(
                    pending,
                    targetPkg.PackageName,
                    durationDays,
                    "A new payment link has been created. Please complete the payment.",
                    ct);
            }

            // Expired or different package expired - cancel and let caller proceed
            await _repo.CancelMarketSubscriptionAsync(pending.Id, ct);
            await _repo.SaveChangesAsync(ct);
            return null;
        }

        private async Task<ApiResponse<PayOSPaymentResponseDto>> RecreatePendingMarketPaymentLinkAsync(
            MarketSubscription pending,
            string packageName,
            int durationDays,
            string message,
            CancellationToken ct)
        {
            if (pending.PayOSOrderCode.HasValue)
            {
                try { await _payos.CancelPaymentLinkAsync(pending.PayOSOrderCode.Value); }
                catch { }
            }

            var orderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.MarketSubscription);
            var payosResp = await _payos.CreatePaymentLinkAsync(
                BuildSubscriptionPaymentRequest(orderCode, pending.PaidAmount, pending.Id, "market"));

            pending.PayOSOrderCode = orderCode;
            pending.PayOSPaymentLinkId = payosResp.PaymentLinkId;
            pending.PaymentExpiresAt = ParsePayOSExpiry(payosResp.ExpiresAt);
            await _repo.SaveChangesAsync(ct);

            return ApiResponse<PayOSPaymentResponseDto>.SuccessResponse(
                MapPayOSResponse(pending.Id, packageName, durationDays, pending.PaidAmount, payosResp, "PendingPaymentResumed"),
                message);
        }

        private static DateTime? ParsePayOSExpiry(DateTimeOffset? expiresAt)
        {
            return expiresAt?.UtcDateTime;
        }

        private PayOSPaymentRequest BuildSubscriptionPaymentRequest(long orderCode, decimal amount, Guid subscriptionId, string flow)
        {
            var returnUrl = flow == "booth" && !string.IsNullOrWhiteSpace(_payOSSettings.BoothReturnUrl)
                ? _payOSSettings.BoothReturnUrl
                : _payOSSettings.ReturnUrl;
            var cancelUrl = flow == "booth" && !string.IsNullOrWhiteSpace(_payOSSettings.BoothCancelUrl)
                ? _payOSSettings.BoothCancelUrl
                : _payOSSettings.CancelUrl;

            return new PayOSPaymentRequest
            {
                OrderCode = orderCode,
                Amount = amount,
                Description = $"SNM {orderCode}",
                ReturnUrl = AppendCallbackContext(returnUrl, flow, subscriptionId),
                CancelUrl = AppendCallbackContext(cancelUrl, flow, subscriptionId),
            };
        }

        private static string AppendCallbackContext(string configuredUrl, string flow, Guid subscriptionId)
        {
            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var callbackUri)
                || (callbackUri.Scheme != Uri.UriSchemeHttp && callbackUri.Scheme != Uri.UriSchemeHttps))
            {
                // Preserve existing non-web callback behaviour (for example mobile deep links).
                return string.Empty;
            }

            var separator = string.IsNullOrEmpty(callbackUri.Query) ? "?" : "&";
            return $"{configuredUrl}{separator}flow={Uri.EscapeDataString(flow)}&subscriptionId={subscriptionId:D}";
        }

        private static PayOSPaymentResponseDto MapPayOSResponse(Guid subscriptionId, string packageName, int durationDays, decimal amount, PayOSPaymentResponse resp, string status = "PendingPayment")
        {
            return new PayOSPaymentResponseDto
            {
                SubscriptionId = subscriptionId,
                OrderCode = resp.OrderCode,
                PackageName = packageName,
                DurationDays = durationDays,
                Amount = amount,
                QrCode = resp.QrCode,
                CheckoutUrl = resp.CheckoutUrl,
                AccountNumber = resp.AccountNumber,
                AccountName = resp.AccountName,
                Description = resp.Description,
                ExpiresAt = resp.ExpiresAt,
                Status = status,
            };
        }

        private static PayOSPaymentResponseDto MapDirectActivationResponse(Guid subscriptionId, string packageName, int durationDays, decimal amount)
        {
            return new PayOSPaymentResponseDto
            {
                SubscriptionId = subscriptionId,
                OrderCode = 0,
                PackageName = packageName,
                DurationDays = durationDays,
                Amount = amount,
                Description = "Subscription activated without payment.",
                Status = "Active"
            };
        }

        private static TimeZoneInfo? _vnTimeZone;

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            if (_vnTimeZone != null)
                return _vnTimeZone;

            string[] ids = { "Asia/Ho_Chi_Minh", "SE Asia Standard Time", "Vietnam Standard Time" };
            foreach (var id in ids)
            {
                try
                {
                    _vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
                    return _vnTimeZone;
                }
                catch { }
            }

            // Fallback: UTC+7
            _vnTimeZone = TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam Standard Time");
            return _vnTimeZone;
        }

        private static int CalculateDaysRemaining(DateTime endDate, SubscriptionStatus status)
        {
            if (status != SubscriptionStatus.Active)
                return 0;

            var vnTimeZone = GetVietnamTimeZone();
            var nowInVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
            var endDateInVn = TimeZoneInfo.ConvertTimeFromUtc(endDate, vnTimeZone);

            var daysRemaining = (endDateInVn.Date - nowInVn.Date).Days;
            return daysRemaining > 0 ? daysRemaining : 0;
        }

        private static CurrentSubscriptionResponse MapBoothCurrent(BoothSubscription sub, bool hasPending, BoothSubscription? pendingInfo = null)
        {
            var daysRemaining = CalculateDaysRemaining(sub.EndDate, sub.Status);
            return new CurrentSubscriptionResponse
            {
                SubscriptionId = sub.Id,
                PackageId = sub.PackageId,
                PackageCode = sub.Package?.Code,
                PackageName = sub.Package?.PackageName ?? "",
                PackageImageUrl = sub.Package?.ImageUrl,
                Status = sub.Status == SubscriptionStatus.Active && sub.StartDate > DateTime.UtcNow
                    ? "Scheduled"
                    : sub.Status.ToString(),
                StartDate = sub.StartDate,
                EndDate = sub.EndDate,
                DaysRemaining = daysRemaining,
                Entitlements = sub.Package?.Entitlements,
                PaidAmount = sub.PaidAmount,
                HasPendingRequest = hasPending,
                PayOSOrderCode = sub.PayOSOrderCode,
                PendingSubscriptionId = pendingInfo?.Id,
                PendingPackageCode = pendingInfo?.Package?.Code,
                PendingPackageName = pendingInfo?.Package?.PackageName,
                PendingStatus = pendingInfo?.Status.ToString(),
                PendingExpiresAt = pendingInfo?.PaymentExpiresAt,
                PendingPaymentExpiresAt = pendingInfo?.PaymentExpiresAt,
            };
        }

        private static CurrentSubscriptionResponse MapMarketCurrent(MarketSubscription sub, bool hasPending, MarketSubscription? pendingInfo = null)
        {
            var daysRemaining = CalculateDaysRemaining(sub.EndDate, sub.Status);
            return new CurrentSubscriptionResponse
            {
                SubscriptionId = sub.Id,
                PackageId = sub.PackageId,
                PackageCode = sub.Package?.Code,
                PackageName = sub.Package?.PackageName ?? "",
                PackageImageUrl = sub.Package?.ImageUrl,
                Status = sub.Status.ToString(),
                StartDate = sub.StartDate,
                EndDate = sub.EndDate,
                DaysRemaining = daysRemaining,
                Entitlements = sub.Package?.Entitlements,
                PaidAmount = sub.PaidAmount,
                HasPendingRequest = hasPending,
                PayOSOrderCode = sub.PayOSOrderCode,
                PendingSubscriptionId = pendingInfo?.Id,
                PendingPackageCode = pendingInfo?.Package?.Code,
                PendingPackageName = pendingInfo?.Package?.PackageName,
                PendingStatus = pendingInfo?.Status.ToString(),
                PendingExpiresAt = pendingInfo?.PaymentExpiresAt,
                PendingPaymentExpiresAt = pendingInfo?.PaymentExpiresAt,
            };
        }

        private static SubscriptionHistoryItem MapHistory(BoothSubscription sub)
        {
            return new SubscriptionHistoryItem
            {
                Id = sub.Id,
                PackageName = sub.Package?.PackageName ?? "",
                PackageCode = sub.Package?.Code,
                PackageImageUrl = sub.Package?.ImageUrl,
                Status = sub.Status == SubscriptionStatus.Active && sub.StartDate > DateTime.UtcNow
                    ? "Scheduled"
                    : sub.Status.ToString(),
                StartDate = sub.StartDate,
                EndDate = sub.EndDate,
                PaidAmount = sub.PaidAmount,
                PayOSOrderCode = sub.PayOSOrderCode,
                PaidAt = sub.PaidAt,
                CreatedAt = sub.CreatedAt,
            };
        }

        private static SubscriptionHistoryItem MapHistory(MarketSubscription sub)
        {
            return new SubscriptionHistoryItem
            {
                Id = sub.Id,
                PackageName = sub.Package?.PackageName ?? "",
                PackageCode = sub.Package?.Code,
                PackageImageUrl = sub.Package?.ImageUrl,
                Status = sub.Status.ToString(),
                StartDate = sub.StartDate,
                EndDate = sub.EndDate,
                PaidAmount = sub.PaidAmount,
                PayOSOrderCode = sub.PayOSOrderCode,
                PaidAt = sub.PaidAt,
                CreatedAt = sub.CreatedAt,
            };
        }
    }
}
