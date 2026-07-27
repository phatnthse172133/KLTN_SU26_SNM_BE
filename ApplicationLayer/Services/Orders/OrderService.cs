using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Services.Promotions;
using ApplicationLayer.Services.CustomerDiscovery;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Models.V1.Payouts.Batch;
using PayOS.Models.Webhooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.Services.Orders
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepo;
        private readonly IPromotionRepository _promotionRepo;
        private readonly IPromotionValidationService _validation;
        private readonly IPayOSPayoutService _payouts;
        private readonly IPayOSService _payos;
        private readonly IRealtimeNotificationPublisher _notificationPublisher;
        private readonly IFoodItemRepository _foodItemRepo;
        private readonly ILogger<OrderService> _logger;
        private readonly IConfiguration _config;
        private readonly IPayOSOrderCodeGenerator _orderCodeGenerator;
        private readonly IBoothRepository _boothRepo;
        private readonly IPromotionUsageRepository _promotionUsages;
        private readonly INotificationService? _notifications;

        public OrderService(IOrderRepository orderRepo,
                            IPromotionRepository promotionRepo,
                            IPromotionValidationService validation,
                            IPayOSPayoutService payouts,
                             IRealtimeNotificationPublisher notificationPublisher,
                             IFoodItemRepository foodItemRepo,
                             ILogger<OrderService> logger,
                             IConfiguration config,
                             IPayOSService payos,
                             IPayOSOrderCodeGenerator orderCodeGenerator,
                             IBoothRepository boothRepo,
                             IPromotionUsageRepository promotionUsages,
                             INotificationService? notifications = null)
        {
            _orderRepo = orderRepo;
            _promotionRepo = promotionRepo;
            _validation = validation;
            _payouts = payouts;
            _notificationPublisher = notificationPublisher;
            _foodItemRepo = foodItemRepo;
            _config = config;
            _logger = logger;
            _payos = payos;
            _orderCodeGenerator = orderCodeGenerator;
            _boothRepo = boothRepo;
            _promotionUsages = promotionUsages;
            _notifications = notifications;
        }

        //DÃƒÂ nh cho customer lÃ¡ÂºÂ«n khÃƒÂ¡ch vang lai (Walk-in) Ã„â€˜Ã¡ÂºÂ·t mÃƒÂ³n, trÃ¡ÂºÂ£ vÃ¡Â»Â link thanh toÃƒÂ¡n nÃ¡ÂºÂ¿u chÃ¡Â»Ân online

        public async Task<ApiResponse<PaginationResp<CustomerOrderHistoryResponse>>> GetCustomerHistoryAsync(
            Guid customerId,
            CustomerOrderHistoryRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
                throw AppException.BadRequest("Order status is invalid.", "INVALID_ORDER_STATUS");

            var page = await _orderRepo.GetCustomerHistoryAsync(customerId, request.Status, request.Page, request.PageSize, cancellationToken);
            var items = page.Items.Select(order => new CustomerOrderHistoryResponse
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                BoothId = order.BoothId,
                BoothName = order.BoothName,
                OrderStatus = order.OrderStatus,
                PaymentStatus = order.PaymentStatus,
                FinalAmount = order.FinalAmount,
                CreatedAt = order.CreatedAt,
                ItemCount = order.ItemCount
            }).ToList();

            return ApiResponse<PaginationResp<CustomerOrderHistoryResponse>>.SuccessResponse(
                PaginationResp<CustomerOrderHistoryResponse>.Create(items, page.TotalCount, request));
        }

        public async Task<ApiResponse<CustomerOrderDetailResponse>> GetCustomerDetailAsync(
            Guid customerId,
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            var order = await _orderRepo.GetCustomerDetailAsync(customerId, orderId, cancellationToken)
                ?? throw AppException.NotFound("Order was not found.", "ORDER_NOT_FOUND");

            return ApiResponse<CustomerOrderDetailResponse>.SuccessResponse(new CustomerOrderDetailResponse
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                Booth = new CustomerOrderBoothResponse { BoothId = order.BoothId, BoothName = order.BoothName },
                Items = order.Items.Select(item => new CustomerOrderItemResponse
                {
                    FoodItemId = item.FoodItemId,
                    FoodName = item.FoodName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal
                }).ToList(),
                Subtotal = order.Subtotal,
                Promotion = order.Promotion is null ? null : new CustomerOrderPromotionResponse
                {
                    PromotionId = order.Promotion.PromotionId,
                    PromotionCode = order.Promotion.PromotionCode,
                    PromotionTitle = order.Promotion.PromotionTitle,
                    DiscountAmount = order.Promotion.DiscountAmount
                },
                DiscountAmount = order.DiscountAmount,
                FinalAmount = order.FinalAmount,
                OrderStatus = order.OrderStatus,
                Payments = order.Payments.Select(payment => new CustomerOrderPaymentResponse
                {
                    PaymentId = payment.PaymentId,
                    Type = payment.Type,
                    Gateway = payment.Gateway,
                    Status = payment.Status,
                    Amount = payment.Amount,
                    RefundAmount = payment.RefundAmount,
                    PaidAt = payment.PaidAt,
                    RefundRequestedAt = payment.RefundRequestedAt,
                    RefundedAt = payment.RefundedAt,
                    CreatedAt = payment.CreatedAt
                }).ToList(),
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            });
        }

        public async Task<ApiResponse<OrderResponseDto>> CreateOrderAsync(CreateOrderDto dto)
        {
            if (dto.Items is null || dto.Items.Count == 0)
                throw AppException.BadRequest("The order must contain at least one item.", "ORDER_ITEMS_REQUIRED");
            if (dto.CheckoutRequestId == Guid.Empty)
                throw AppException.BadRequest("CheckoutRequestId is required.", "CHECKOUT_REQUEST_ID_REQUIRED");
            if (dto.Items.Any(item => item.FoodItemId == Guid.Empty || item.Quantity <= 0))
                throw AppException.BadRequest("Every order item must have a valid food id and quantity.", "INVALID_ORDER_ITEM");
            if (dto.Items.Select(item => item.FoodItemId).Distinct().Count() != dto.Items.Count)
                throw AppException.BadRequest("Duplicate food items are not allowed.", "DUPLICATE_ORDER_ITEM");

            var customerId = dto.CustomerId;
            if (dto.IsCreatedByBooth && customerId is null)
                customerId = Guid.Parse(_config["SystemSettings:WalkInCustomerId"]
                    ?? "00000000-0000-0000-0000-000000000001");
            if (customerId is null)
                throw AppException.BadRequest("Customer id is required.", "CUSTOMER_ID_REQUIRED");

            var utcNow = DateTime.UtcNow;
            var foodIds = dto.Items.Select(item => item.FoodItemId).ToList();
            var foods = await _foodItemRepo.GetAllFoodItemsByIdsAsync(foodIds);
            if (foods.Count != foodIds.Count)
                throw AppException.NotFound("One or more food items do not exist.", "FOOD_ITEM_NOT_FOUND");

            var foodsById = foods.ToDictionary(food => food.Id);
            var boothIds = foods.Select(food => food.BoothId).Distinct().ToList();
            if (boothIds.Count != 1 || boothIds[0] != dto.BoothId)
                throw AppException.BadRequest("All order items must belong to the selected booth.", "MULTIPLE_BOOTHS_NOT_ALLOWED");

            var booth = foods[0].Booth;
            if (booth.BoothOwnerId != dto.BoothOwnerId)
                throw AppException.BadRequest("The booth information is invalid.", "BOOTH_MISMATCH");

            foreach (var item in dto.Items)
            {
                var food = foodsById[item.FoodItemId];
                var orderability = CustomerOrderability.Evaluate(food, utcNow);
                if (!orderability.CanOrder)
                    throw AppException.Conflict(
                        CustomerOrderability.GetPublicMessage(orderability.ReasonCode!),
                        orderability.ReasonCode!);
                if (item.UnitPrice != FoodPriceResolver.GetCurrentPrice(food, utcNow))
                    throw AppException.Conflict(
                        $"The price of '{food.Name}' has changed. Refresh the cart.",
                        "PRICE_CHANGED");
            }

            var orderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.Order);
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId.Value,
                BoothOwnerId = booth.BoothOwnerId,
                OrderCode = orderCode,
                CheckoutRequestId = dto.CheckoutRequestId,
                Note = dto.Note,
                Status = OrderStatus.Placed,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            foreach (var item in dto.Items)
            {
                var food = foodsById[item.FoodItemId];
                var unitPrice = FoodPriceResolver.GetCurrentPrice(food, utcNow);
                order.OrderDetails.Add(new OrderDetail
                {
                    Id = Guid.NewGuid(),
                    FoodItemId = food.Id,
                    FoodNameSnapshot = food.Name,
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice * item.Quantity,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            }

            order.TotalAmount = order.OrderDetails.Sum(detail => detail.TotalPrice);
            Promotion? promotion = null;
            IReadOnlyCollection<CartItem>? validationItems = null;
            if (!string.IsNullOrWhiteSpace(dto.PromotionCode))
            {
                promotion = await _promotionRepo.GetByCodeAsync(dto.BoothId, dto.PromotionCode)
                    ?? throw AppException.NotFound("Promotion was not found.", "PROMOTION_NOT_FOUND");
                validationItems = dto.Items.Select(item => new CartItem
                {
                    FoodItemId = item.FoodItemId,
                    Quantity = item.Quantity,
                    FoodItem = foodsById[item.FoodItemId]
                }).ToList();
                var preview = await _validation.ValidateAsync(customerId.Value, promotion, validationItems);
                order.DiscountAmount = preview.DiscountAmount;
                order.PromotionUsages.Add(new PromotionUsage
                {
                    Id = Guid.NewGuid(),
                    PromotionId = promotion.Id,
                    OrderId = order.Id,
                    CustomerId = customerId.Value,
                    DiscountAmount = preview.DiscountAmount,
                    PromotionCodeSnapshot = promotion.PromotionCode,
                    PromotionTitleSnapshot = promotion.Title,
                    Status = PromotionUsageStatus.Reserved,
                    AppliedAt = utcNow,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            }

            if (order.TotalAmount < 0m
                || order.DiscountAmount < 0m
                || order.DiscountAmount > order.TotalAmount)
            {
                throw AppException.Conflict(
                    "The order financial totals are invalid. Refresh the cart and retry.",
                    "ORDER_FINANCIAL_INVARIANT_VIOLATION");
            }

            order.FinalAmount = order.TotalAmount - order.DiscountAmount;
            var isZeroPaymentOrder = order.FinalAmount == 0m;
            if (isZeroPaymentOrder)
            {
                // A fully discounted order is financially settled without an
                // external provider. It follows the same operational state as
                // a successfully paid PayOS order.
                order.Status = OrderStatus.Preparing;
                PromotionUsageLifecycle.ConsumeReserved(
                    order.PromotionUsages,
                    utcNow);
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                BoothOwnerId = booth.BoothOwnerId,
                Amount = order.FinalAmount,
                Type = dto.PaymentMethod,
                Gateway = isZeroPaymentOrder
                    ? PaymentGateway.None
                    : dto.PaymentMethod == PaymentType.PayOS
                        ? PaymentGateway.Payos
                        : PaymentGateway.BankTransfer,
                Status = isZeroPaymentOrder ? PaymentStatus.Paid : PaymentStatus.Pending,
                PayOSOrderCode = dto.PaymentMethod == PaymentType.PayOS && !isZeroPaymentOrder
                    ? orderCode
                    : null,
                PaidAt = isZeroPaymentOrder ? utcNow : null,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
            order.Payments.Add(payment);

            string? checkoutUrl = null;
            var payosLinkCreated = false;
            await _orderRepo.BeginTransactionAsync();
            try
            {
                await _orderRepo.AcquireCheckoutLockAsync(customerId.Value, dto.CheckoutRequestId);
                var existingOrder = await _orderRepo.GetByCheckoutRequestAsync(customerId.Value, dto.CheckoutRequestId);
                if (existingOrder is not null)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    var existingPayment = existingOrder.Payments
                        .OrderByDescending(existing => existing.CreatedAt)
                        .FirstOrDefault();
                    return ApiResponse<OrderResponseDto>.SuccessResponse(new OrderResponseDto
                    {
                        OrderId = existingOrder.Id,
                        OrderCode = existingOrder.OrderCode,
                        Status = existingOrder.Status,
                        PaymentUrl = existingPayment?.CheckoutUrl
                    }, "The existing idempotent checkout result was returned.");
                }

                if (promotion is not null)
                {
                    await _promotionRepo.AcquireReservationLockAsync(promotion.Id);
                    var lockedPromotion = await _promotionRepo.GetReservationDetailsAsync(promotion.Id)
                        ?? throw AppException.Conflict(
                            "Promotion is no longer available. Please retry.",
                            "PROMOTION_CHANGED");
                    var finalValidation = await _validation.ValidateAsync(
                        customerId.Value,
                        lockedPromotion,
                        validationItems!);
                    if (finalValidation.DiscountAmount != order.DiscountAmount)
                        throw AppException.Conflict(
                            "Promotion terms changed during checkout. Please retry.",
                            "PROMOTION_CHANGED");
                }

                // Promotion quota must be finalized under its PostgreSQL row lock
                // before creating an external money intent. Otherwise two different
                // checkout keys can both create PayOS links while only one may reserve
                // the last promotion slot.
                if (dto.PaymentMethod == PaymentType.PayOS && !isZeroPaymentOrder)
                {
                    var link = await _payos.CreatePaymentLinkAsync(new PayOSPaymentRequest
                    {
                        OrderCode = orderCode,
                        Amount = order.FinalAmount,
                        Description = $"SNM{orderCode % 1_000_000}"
                    });
                    checkoutUrl = link.CheckoutUrl;
                    payment.CheckoutUrl = link.CheckoutUrl;
                    payment.PaymentLinkId = link.PaymentLinkId;
                    payosLinkCreated = true;
                }

                await _orderRepo.AddAsync(order);
                await _orderRepo.SaveChangesAsync();
                await _orderRepo.CommitTransactionAsync();
            }
            catch
            {
                await _orderRepo.RollbackTransactionAsync();
                if (payosLinkCreated)
                    await TryCancelPayOSLinkAsync(orderCode);
                throw;
            }

            if (dto.PaymentMethod == PaymentType.Cash || isZeroPaymentOrder)
                await TryPublishOrderCreatedAsync(order, utcNow, isZeroPaymentOrder);

            return ApiResponse<OrderResponseDto>.SuccessResponse(new OrderResponseDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                Status = order.Status,
                PaymentUrl = checkoutUrl
            }, "Order created successfully.");
        }


        public async Task<ApiResponse<SupplementalPaymentResponseDto>> PayRemainingAmountAsync(
            Guid actorId,
            long orderCode)
        {
            throw AppException.Conflict(
                "Supplemental PayOS payments are disabled because the current model cannot preserve both expected and actually received amounts.",
                "SUPPLEMENTAL_PAYMENT_NOT_SUPPORTED");
#pragma warning disable CS0162
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode)
                ?? throw AppException.NotFound("Order was not found.", "ORDER_NOT_FOUND");
            if (order.CustomerId != actorId && order.BoothOwnerId != actorId)
                throw AppException.Forbidden("You cannot access this order.", "ORDER_ACCESS_DENIED");
            if (order.Status != OrderStatus.Underpaid)
                throw AppException.Conflict("Order is not underpaid.", "ORDER_NOT_UNDERPAID");

            await _orderRepo.BeginTransactionAsync();
            long? createdCode = null;
            try
            {
                await _orderRepo.AcquireSupplementalPaymentLockAsync(order.Id);
                var existing = await _orderRepo.GetPendingPayOSPaymentByOrderIdAsync(order.Id);
                var totalPaid = await _orderRepo.GetTotalPaidAmountAsync(order.OrderCode);
                var remaining = Math.Max(order.FinalAmount - totalPaid, 0m);
                if (remaining <= 0)
                    throw AppException.Conflict("Order has no remaining balance.", "ORDER_ALREADY_PAID");

                if (existing is not null && !string.IsNullOrWhiteSpace(existing.CheckoutUrl))
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return ApiResponse<SupplementalPaymentResponseDto>.SuccessResponse(new()
                    {
                        OrderId = order.Id,
                        OrderCode = order.OrderCode,
                        RemainingAmount = existing.Amount,
                        TotalPaid = totalPaid,
                        FinalAmount = order.FinalAmount,
                        PaymentUrl = existing.CheckoutUrl,
                        PayOSOrderCode = existing.PayOSOrderCode ?? 0
                    });
                }

                createdCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.Order);
                var link = await _payos.CreatePaymentLinkAsync(new PayOSPaymentRequest
                {
                    OrderCode = createdCode.Value,
                    Amount = remaining,
                    Description = $"SNM{createdCode.Value % 1_000_000}"
                });
                var now = DateTime.UtcNow;
                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    BoothOwnerId = order.BoothOwnerId,
                    Amount = remaining,
                    Type = PaymentType.PayOS,
                    Gateway = PaymentGateway.Payos,
                    Status = PaymentStatus.Pending,
                    CheckoutUrl = link.CheckoutUrl,
                    PaymentLinkId = link.PaymentLinkId,
                    PayOSOrderCode = createdCode.Value,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _orderRepo.AddPaymentAsync(payment);
                await _orderRepo.SaveChangesAsync();
                await _orderRepo.CommitTransactionAsync();

                return ApiResponse<SupplementalPaymentResponseDto>.SuccessResponse(new()
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    RemainingAmount = remaining,
                    TotalPaid = totalPaid,
                    FinalAmount = order.FinalAmount,
                    PaymentUrl = link.CheckoutUrl,
                    PayOSOrderCode = createdCode.Value
                });
            }
            catch
            {
                await _orderRepo.RollbackTransactionAsync();
                if (createdCode.HasValue)
                    await TryCancelPayOSLinkAsync(createdCode.Value);
                throw;
            }
#pragma warning restore CS0162
        }


        public async Task<WebhookDispatchResult> ProcessPaymentWebhookAsync(
            PayOSWebhookData verifiedData)
        {
            if (verifiedData is null)
                return WebhookDispatchResult.InvalidSignature;
            if (!verifiedData.IsSuccessful)
                return WebhookDispatchResult.NotSuccessful;

            // Resolve supplemental provider codes without tracking an Order, then take
            // the Order row lock before reading or mutating any state. Cleanup and both
            // cancellation paths use the same Order -> Payment lock order.
            var orderCode = await _orderRepo.GetOrderCodeByPayOSOrderCodeAsync(verifiedData.OrderCode)
                ?? verifiedData.OrderCode;
            var now = DateTime.UtcNow;
            await _orderRepo.BeginTransactionAsync();
            try
            {
                var order = await _orderRepo.GetOrderByCodeForUpdateAsync(orderCode);
                if (order is null)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return WebhookDispatchResult.NotFound;
                }

                var targetPayment = order.Payments
                    .Where(payment => payment.PayOSOrderCode == verifiedData.OrderCode
                        || (payment.PayOSOrderCode is null && order.OrderCode == verifiedData.OrderCode))
                    .OrderByDescending(payment => payment.CreatedAt)
                    .FirstOrDefault();
                if (targetPayment is null)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return WebhookDispatchResult.NotFound;
                }

                if (!string.IsNullOrWhiteSpace(targetPayment.PaymentLinkId)
                    && !string.IsNullOrWhiteSpace(verifiedData.PaymentLinkId)
                    && !string.Equals(targetPayment.PaymentLinkId, verifiedData.PaymentLinkId, StringComparison.Ordinal))
                {
                    await _orderRepo.RollbackTransactionAsync();
                    _logger.LogError(
                        "PayOS webhook payment-link mismatch for provider order {OrderCode}.",
                        verifiedData.OrderCode);
                    return WebhookDispatchResult.Conflict;
                }

                if (order.Status == OrderStatus.Cancelled)
                {
                    if (targetPayment.Status is PaymentStatus.Pending or PaymentStatus.Cancelled)
                    {
                        targetPayment.Status = PaymentStatus.RefundProcessing;
                        targetPayment.GatewayRef = verifiedData.Reference;
                        targetPayment.RefundAmount = verifiedData.Amount;
                        targetPayment.RefundReference = $"refund-{targetPayment.Id:N}";
                        targetPayment.RefundReason = "Verified payment received after order cancellation.";
                        targetPayment.RefundRequestedAt = now;
                        targetPayment.UpdatedAt = now;
                        await _orderRepo.SaveChangesAsync();
                    }
                    await _orderRepo.CommitTransactionAsync();
                    return WebhookDispatchResult.OrderHandled;
                }

                if (order.Status is OrderStatus.Preparing
                    or OrderStatus.ReadyForPickup
                    or OrderStatus.Completed)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return WebhookDispatchResult.AlreadyProcessed;
                }

                var amountMatches = verifiedData.Amount == targetPayment.Amount;
                var paymentRows = amountMatches
                    ? await _orderRepo.UpdatePendingPaymentStatusByIdAsync(
                        targetPayment.Id,
                        PaymentStatus.Paid,
                        verifiedData.Reference,
                        now,
                        now)
                    : await _orderRepo.MarkPendingPaymentForRefundAsync(
                        targetPayment.Id,
                        verifiedData.Amount,
                        verifiedData.Reference,
                        now);
                if (paymentRows == 0)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return WebhookDispatchResult.AlreadyProcessed;
                }

                if (!amountMatches)
                {
                    var cancelledRows = await _orderRepo.UpdateOrderStatusIfPlacedAsync(
                        order.OrderCode,
                        OrderStatus.Cancelled,
                        now);
                    if (cancelledRows == 0)
                    {
                        await _orderRepo.RollbackTransactionAsync();
                        return WebhookDispatchResult.AlreadyProcessed;
                    }

                    await _promotionUsages.ConsumeReservedByOrderAsync(order.Id, now);
                    await _orderRepo.CommitTransactionAsync();
                    _logger.LogError(
                        "PayOS amount mismatch for provider order {ProviderOrderCode}. Expected {ExpectedAmount}, received {ActualAmount}; payment moved to refund processing.",
                        verifiedData.OrderCode,
                        targetPayment.Amount,
                        verifiedData.Amount);
                    return WebhookDispatchResult.OrderHandled;
                }

                var orderRows = await _orderRepo.UpdateOrderStatusIfPlacedAsync(
                    order.OrderCode,
                    OrderStatus.Preparing,
                    now);

                if (orderRows == 0)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return WebhookDispatchResult.AlreadyProcessed;
                }

                await _promotionUsages.ConsumeReservedByOrderAsync(order.Id, now);
                await _orderRepo.CommitTransactionAsync();
            }
            catch
            {
                await _orderRepo.RollbackTransactionAsync();
                throw;
            }

            var completedOrder = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (completedOrder is not null)
                await TryPublishPaymentSucceededAsync(completedOrder, now);
            return WebhookDispatchResult.OrderHandled;
        }

#if false // PayOS Webhook is a PayIn contract and must never finalize a payout.
        public async Task<bool> ProcessPayoutWebhookAsync(Webhook webhookBody)
        {
            // 1. Kiểm tra tính hợp lệ của Webhook (Verify chữ ký/checksum của PayOS để tránh hacker giả lập)
            WebhookData verifiedData = await _payOutClient.Webhooks.VerifyAsync(webhookBody);
            if (verifiedData == null)
            {
                _logger.LogWarning("Webhook nhận được dữ liệu không hợp lệ hoặc chữ ký giả mạo.");
                return true;
            }

            // 2. Trích xuất thông tin mã đơn hàng từ referenceId 
            // referenceId dạng: "refund_123456_ticks" -> tách chuỗi lấy 123456
            if (string.IsNullOrEmpty(verifiedData.Reference))
            {
                _logger.LogWarning("Webhook Payout không chứa thông tin reference.");
                return true;
            }

            var parts = verifiedData.Reference.Split('_');
            if (parts.Length < 2 || !long.TryParse(parts[1], out long orderCode))
            {
                _logger.LogWarning($"Webhook Payout nhận được referenceId không hợp lệ: {verifiedData.Reference}");
                return true;
            }

            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order == null)
            {
                _logger.LogWarning($"Không tìm thấy đơn hàng nào khớp với OrderCode: {orderCode} từ Webhook Payout.");
                return true;
            }

            var payment = order.Payments
                               .OrderByDescending(p => p.CreatedAt)
                               .FirstOrDefault(p => p.Status == PaymentStatus.RefundProcessing);

            if (payment != null)
            {
                // 3. Nếu PayOS báo lệnh Payout thành công -> Chuyển sang Refunded
                if (verifiedData.Description != null && verifiedData.Description.ToLower() == "success")
                {
                    payment.Status = PaymentStatus.Refunded;
                    payment.UpdatedAt = DateTime.UtcNow;

                    _logger.LogInformation($"Webhook: Đơn hàng #{orderCode} đã được hoàn tiền THÀNH CÔNG.");
                }
                // Nếu PayOS báo lệnh Payout thất bại (ví dụ tài khoản đích bị khóa ngầm)
                else
                {
                    payment.Status = PaymentStatus.Paid; // Trả về Paid vì thực tế tiền vẫn đang ở ví của quán, chưa đi được
                    payment.UpdatedAt = DateTime.UtcNow;

                    _logger.LogError($"Webhook: Lỗi hoàn tiền đơn #{orderCode}");
                }

                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning($"Tìm thấy đơn #{orderCode} nhưng không có bản ghi thanh toán nào ở trạng thái chờ hoàn tiền.");
            }

            return true; // Trả về 200 để báo cho PayOS biết hệ thống đã nhận được dữ liệu
        }

#endif
        //public async Task<bool> RejectOrderAsync(RejectOrderDto dto)
        //{
        //    // 1. Tìm đơn hàng cần hủy trong Database
        //    var order = await _orderRepo.GetByIdAsync(dto.OrderId);
        //    if (order == null) throw new Exception("Không tìm thấy đơn hàng!");

        //    // 2. Kiểm tra trạng thái: Chỉ được từ chối khi đơn hàng mới đặt (Placed hoặc PendingPayment)
        //    if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
        //    {
        //        throw new Exception("Đơn hàng đã hoàn thành hoặc đã bị hủy trước đó, không thể từ chối!");
        //    }

        //    // 3. XỬ LÝ RẼ NHÁNH DÒNG TIỀN:

        //    // TRƯỜNG HỢP 1: Khách đặt bằng TIỀN MẶT
        //    if (order.PayStatus == PayOrderStatus.Pending)
        //    {
        //        order.Status = OrderStatus.Cancelled;
        //        order.Note = dto.Reason; // Lưu vết lý do hủy
        //        order.UpdatedAt = DateTime.UtcNow;
        //        order.PayStatus = PayOrderStatus.Failed;

        //        // Cập nhật bảng Payment sang trạng thái thất bại/hủy
        //        var payment = order.Payments.FirstOrDefault(p => p.Status == PaymentStatus.Pending);
        //        if (payment != null) payment.Status = PaymentStatus.Failed;
        //    }
        //    // TRƯỜNG HỢP 2: Khách đặt ONLINE và trạng thái đã báo ĐÃ THANH TOÁN (Paid = 1)
        //    else if (order.PayStatus == PayOrderStatus.Paid)
        //    {
        //        // Bước A: Đổi trạng thái đơn hàng sang "Đang chờ hoàn tiền"
        //        order.Status = OrderStatus.Cancelled;
        //        order.PayStatus = PayOrderStatus.RefundPending; 
        //        order.Note = dto.Reason;
        //        order.UpdatedAt = DateTime.UtcNow;

        //        // Tìm bản ghi lịch sử giao dịch thành công trước đó để lấy thông tin đối chiếu
        //        var successPayment = order.Payments.FirstOrDefault(p => p.Status == PaymentStatus.Paid);

        //        // Bước B: Gọi API kích hoạt lệnh hoàn tiền tự động sang phía PayOS
        //        try
        //        {
        //            // Sinh mã đơn kiểu long phục vụ PayOS
        //            long orderCodeLong = long.Parse(order.OrderCode);

        //            // Khởi tạo Object cấu hình lệnh hoàn tiền theo SDK PayOS mới nhất
        //            var refundRequest = new RefundRequest(
        //                amount: (int)order.FinalAmount, // Số tiền cần trả lại cho khách
        //                description: $"Hoan tien don #{order.OrderCode.Substring(0, 5)} do chu quay tu choi"
        //            );

        //            // Tiến hành gọi lệnh lên mây của PayOS
        //            RefundResult refundResult = await _payOSClient.(orderCodeLong, refundRequest);

        //            if (refundResult.Status == "REJECTED") // Phía ngân hàng/PayOS xử lý xong ngay lập tức
        //            {
        //                // Bước C: Hoàn tiền thành công mỹ mãn -> Đổi sang trạng thái Đã hoàn tiền
        //                order.PayStatus = PayOrderStatus.Refunded; // Thuộc tính Refunded = 3 của bạn

        //                if (successPayment != null)
        //                {
        //                    successPayment.Status = PaymentStatus.Refunded; // Lưu vết bảng lịch sử giao dịch
        //                    successPayment.UpdatedAt = DateTime.UtcNow;
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            // Nếu có lỗi mạng hoặc lỗi từ phía PayOS, đơn hàng vẫn treo ở dạng "RefundPending" để Admin vào xử lý tay sau
        //            await _orderRepo.UpdateOrderAsync(order);
        //            await _orderRepo.SaveChangesAsync();
        //            throw new Exception($"Chủ quầy từ chối đơn thành công, nhưng lệnh hoàn tiền tự động PayOS gặp sự cố: {ex.Message}");
        //        }
        //    }

        //    // 4. Lưu toàn bộ thay đổi cập nhật trạng thái vào Database
        //    await _orderRepo.UpdateOrderAsync(order);
        //    await _orderRepo.SaveChangesAsync();

        //    // 5. REAL-TIME (SỬ DỤNG KÉ KHUNG CỦA CHỦ NHÓM):
        //    // Bắn thông báo ngược lại cho máy của KHÁCH HÀNG (order.CustomerId) để thông báo tin buồn đơn bị hủy
        //    var notificationPayload = new NotificationListItemResponse
        //    {
        //        Id = Guid.NewGuid(),
        //        BoothId = order.BoothOwnerId,
        //        Type = "ORDER_REJECTED", // Mã riêng để FE xử lý đổi màu đỏ
        //        Title = "Đơn hàng bị từ chối",
        //        Content = order.PayStatus == PayOrderStatus.Refunded
        //            ? $"Rất tiếc, quầy đã từ chối đơn #{order.OrderCode} của bạn do: {dto.Reason}. Tiền đã được hoàn lại ví của bạn!"
        //            : $"Rất tiếc, quầy đã từ chối đơn #{order.OrderCode} của bạn do: {dto.Reason}.",
        //        IsRead = false,
        //        ReferenceType = "Order",
        //        ReferenceId = order.Id,
        //        CreatedAt = DateTime.UtcNow
        //    };

        //    // Bắn đích danh đến máy của Customer thông qua Realtime Publisher có sẵn của nhóm
        //    await _notificationPublisher.PublishAsync(order.CustomerId, notificationPayload, unreadCount: 1);

        //    return true;
        //}

        public async Task<ApiResponse<bool>> UpdateOrderStatusByBoothOwnerAsync(Guid boothOwnerId, UpdateOrderStatusDto dto)
        {
            // 1. Láº¥y Ä‘Æ¡n hÃ ng lÃªn kÃ¨m thÃ´ng tin giao dá»‹ch Ä‘á»ƒ kiá»ƒm tra dÃ²ng tiá»n
            var order = await _orderRepo.GetOrderByCodeAsync(dto.OrderCode);
            if (order == null) return ApiResponse<bool>.Failure("KhÃ´ng tÃ¬m tháº¥y Ä‘Æ¡n hÃ ng!");

            // 2. Báº¢O Máº¬T: Kiá»ƒm tra xem Ä‘Æ¡n nÃ y cÃ³ thuá»™c vá» quáº§y cá»§a Ã´ng nÃ y khÃ´ng
            if (order.BoothOwnerId != boothOwnerId)
            {
                return ApiResponse<bool>.Failure("BÃ¡ÂºÂ¡n khÃƒÂ´ng cÃƒÂ³ quyÃ¡Â»Ân chÃ¡Â»â€°nh sÃ¡Â»Â­a Ã„â€˜Ã†Â¡n hÃƒÂ ng cÃ¡Â»Â§a quÃ¡ÂºÂ§y khÃƒÂ¡c!");
            }

            if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Completed)
            {
                return ApiResponse<bool>.Failure($"ÄÆ¡n hÃ ng Ä‘Ã£ Ä‘Ã³ng (Tráº¡ng thÃ¡i hiá»‡n táº¡i: {order.Status}). KhÃ´ng thá»ƒ chá»‰nh sá»­a thÃªm.");
            }

            //Xá»­ lÃ½ dá»±a trÃªn loáº¡i thanh toÃ¡n: Náº¿u lÃ  tiá»n máº·t thÃ¬ khi quáº§y báº¥m "HoÃ n thÃ nh" thÃ¬ tá»± Ä‘á»™ng cáº­p nháº­t Payment sang Paid, náº¿u lÃ  PayOS thÃ¬ pháº£i chá» Webhook tá»« PayOS vá» má»›i Ä‘Æ°á»£c phÃ©p hoÃ n thÃ nh
            var transitionAllowed = (order.Status, dto.NewStatus) switch
            {
                (OrderStatus.Placed, OrderStatus.Preparing) => true,
                (OrderStatus.Preparing, OrderStatus.ReadyForPickup) => true,
                (OrderStatus.ReadyForPickup, OrderStatus.Completed) => true,
                _ => false
            };
            if (!transitionAllowed)
            {
                return ApiResponse<bool>.Failure(
                    $"Invalid order transition: {order.Status} -> {dto.NewStatus}.",
                    "INVALID_ORDER_STATUS_TRANSITION",
                    false);
            }

            var payment = order.Payments.OrderByDescending(p => p.CreatedAt)
                                            .FirstOrDefault();

            if (payment == null)
            {
                return ApiResponse<bool>.Failure("KhÃ´ng tÃ¬m tháº¥y thÃ´ng tin thanh toÃ¡n cá»§a Ä‘Æ¡n hÃ ng!");
            }

            if (dto.NewStatus == OrderStatus.Preparing)
            {
                if (payment.Type == PaymentType.PayOS)
                {
                    // Náº¿u khÃ¡ch tráº£ thiáº¿u -> Chá»§ quÃ¡n báº¥m nÃºt nÃ y Ä‘á»“ng nghÄ©a vá»›i viá»‡c CHáº¤P NHáº¬N BÃ™ TIá»€N THIáº¾U
                    if (payment.Status == PaymentStatus.Underpaid)
                        return ApiResponse<bool>.Failure("Underpaid PayOS orders require manual refund and cannot be accepted.");
                    // Náº¿u khÃ¡ch chÆ°a thanh toÃ¡n Ä‘á»“ng nÃ o -> CHáº¶N TUYá»†T Äá»I khÃ´ng cho lÃ m mÃ³n
                    else if (payment.Status != PaymentStatus.Paid)
                    {
                        return ApiResponse<bool>.Failure("KhÃ¡ch Ä‘áº·t online chÆ°a thanh toÃ¡n thÃ nh cÃ´ng. KhÃ´ng thá»ƒ duyá»‡t lÃ m mÃ³n!");
                    }
                }
            }

            // 3. Chá»‘ng gian láº­n tiá»n báº¡c
            if (dto.NewStatus == OrderStatus.Completed)
            {
                // Kiá»ƒm tra xem Ä‘Æ¡n nÃ y cÃ³ báº£n ghi thanh toÃ¡n thÃ nh cÃ´ng nÃ o chÆ°a
                bool isPaid = order.Payments.Any(p => p.Status == PaymentStatus.Paid);
                if (!isPaid)
                {
                    return ApiResponse<bool>.Failure("KhÃ´ng thá»ƒ hoÃ n thÃ nh Ä‘Æ¡n hÃ ng chÆ°a Ä‘Æ°á»£c thanh toÃ¡n thÃ nh cÃ´ng!");
                }
            }

            // 4. Cáº­p nháº­t tráº¡ng thÃ¡i
            order.Status = dto.NewStatus;
            order.UpdatedAt = DateTime.UtcNow;

            if (payment.Status == PaymentStatus.Paid
                && dto.NewStatus is OrderStatus.Preparing or OrderStatus.Completed)
            {
                PromotionUsageLifecycle.ConsumeReserved(
                    order.PromotionUsages,
                    order.UpdatedAt);
            }

            _orderRepo.Update(order);
            await _orderRepo.SaveChangesAsync();

            // 5. Xá»¬ LÃ REALTIME "TING TING" QUA SIGNALR
            // Chá»‰ báº¯n tin cho khÃ¡ch hÃ ng náº¿u Ä‘Ã¢y lÃ  khÃ¡ch Ä‘áº·t qua App (cÃ³ CustomerId cá»¥ thá»ƒ)
            // Náº¿u lÃ  ID khÃ¡ch vÃ£ng lai (toÃ n sá»‘ 0) thÃ¬ bá» qua khÃ´ng cáº§n báº¯n
            var walkInId = Guid.Parse(_config["SystemSettings:WalkInCustomerId"] ?? "00000000-0000-0000-0000-000000000001");

            if (order.CustomerId != Guid.Empty && order.CustomerId != walkInId)
            {
                string title = "";
                string content = "";

                // TÃ¹y biáº¿n ná»™i dung tin nháº¯n dá»±a theo tá»«ng tráº¡ng thÃ¡i mÃ³n Äƒn
                switch (order.Status)
                {
                    case OrderStatus.Preparing:
                        title = "ÄÆ¡n hÃ ng Ä‘ang Ä‘Æ°á»£c cháº¿ biáº¿n!";
                        content = $"Quáº§y Ä‘Ã£ tiáº¿p nháº­n vÃ  Ä‘ang lÃ m mÃ³n cho Ä‘Æ¡n # {order.OrderCode} cá»§a báº¡n.";
                        break;
                    case OrderStatus.ReadyForPickup:
                        title = "MÃ³n Äƒn Ä‘Ã£ sáºµn sÃ ng! ðŸ¥³";
                        content = $"ÄÆ¡n hÃ ng # {order.OrderCode} Ä‘Ã£ lÃ m xong. Báº¡n hÃ£y Ä‘áº¿n quáº§y Ä‘á»ƒ nháº­n mÃ³n nhÃ©!";
                        break;
                    case OrderStatus.Completed:
                        title = "Cáº£m Æ¡n báº¡n Ä‘Ã£ mua hÃ ng! â¤ï¸";
                        content = $"ÄÆ¡n hÃ ng # {order.OrderCode} Ä‘Ã£ Ä‘Æ°á»£c giao thÃ nh cÃ´ng. ChÃºc báº¡n ngon miá»‡ng!";
                        break;
                }

                if (!string.IsNullOrEmpty(title))
                {
                    var notificationType = order.Status switch
                    {
                        OrderStatus.Preparing => NotificationType.OrderPreparing,
                        OrderStatus.ReadyForPickup => NotificationType.OrderReady,
                        OrderStatus.Completed => NotificationType.OrderCompleted,
                        _ => NotificationType.Order
                    };

                    // BÃ¡ÂºÂ¯n Ã„â€˜ÃƒÂ­ch danh vÃƒÂ o Group SignalR cÃ¡Â»Â§a khÃƒÂ¡ch hÃƒÂ ng (TÃƒÂªn group chÃƒÂ­nh lÃƒÂ  CustomerId)
                    await PublishPersistedNotificationAsync(
                        order.CustomerId,
                        notificationType,
                        title,
                        content,
                        order.Id);
                }
            }

            return ApiResponse<bool>.SuccessResponse(true, "Cáº­p nháº­t tráº¡ng thÃ¡i Ä‘Æ¡n hÃ ng thÃ nh cÃ´ng!");
        }

        //KhÃ¡ch chá»§ Ä‘á»™ng há»§y Ä‘Æ¡n hÃ ng trÆ°á»›c khi quáº§y nháº­n Ä‘Æ¡n (Chá»‰ Ã¡p dá»¥ng cho khÃ¡ch Ä‘áº·t qua App, khÃ´ng Ã¡p dá»¥ng cho khÃ¡ch vÃ£ng lai)
        public async Task<ApiResponse<bool>> CancelOrderByCustomer(Guid customerId, long orderCode)
        {
            await _orderRepo.BeginTransactionAsync();
            var order = await _orderRepo.GetOrderByCodeForUpdateAsync(orderCode);
            if (order is not null && order.CustomerId != customerId)
            {
                await _orderRepo.RollbackTransactionAsync();
                throw AppException.Forbidden("You cannot cancel another customer's order.", "ORDER_ACCESS_DENIED");
            }
            if (order is null)
            {
                await _orderRepo.RollbackTransactionAsync();
                return ApiResponse<bool>.Failure("Order does not exist.", "ORDER_NOT_FOUND", false);
            }
            if (order == null) return ApiResponse<bool>.Failure("Ã„ÂÃ†Â¡n hÃƒÂ ng khÃƒÂ´ng tÃ¡Â»â€œn tÃ¡ÂºÂ¡i", data: false);

            // ChÃ¡Â»â€° cho phÃƒÂ©p hÃ¡Â»Â§y khi Ã„â€˜Ã†Â¡n Ã„â€˜ang Ã¡Â»Å¸ trÃ¡ÂºÂ¡ng thÃƒÂ¡i chÃ¡Â»Â thanh toÃƒÂ¡n (Pending)

            // Chá»‰ cho phÃ©p há»§y khi Ä‘Æ¡n Ä‘ang á»Ÿ tráº¡ng thÃ¡i chá» thanh toÃ¡n (Pending)
            
            if (order.Status != OrderStatus.Placed)
            {
                await _orderRepo.RollbackTransactionAsync();
                return ApiResponse<bool>.Failure($"Ã„ÂÃ†Â¡n hÃƒÂ ng khÃƒÂ´ng thÃ¡Â»Æ’ hÃ¡Â»Â§y Ã¡Â»Å¸ trÃ¡ÂºÂ¡ng thÃƒÂ¡i {order.Status}", data: false);
            }

            var payment = order.Payments
                               .OrderByDescending(p => p.CreatedAt)
                               .FirstOrDefault(); // láº¥y cÃ¡i Ä‘áº§u tiÃªn

            if (payment == null)
            {
                await _orderRepo.RollbackTransactionAsync();
                return ApiResponse<bool>.Failure("KhÃ´ng tÃ¬m tháº¥y báº£n ghi thanh toÃ¡n Pending Ä‘á»ƒ há»§y Ä‘Æ¡n", data: false);
            }

            try
            {
                try
                {
                    // Chá»§ Ä‘á»™ng gá»i PayOS Ä‘Ã³ng link thanh toÃ¡n, cháº·n khÃ´ng cho quÃ©t QR ná»¯a
                    await _payos.CancelPaymentLinkAsync(order.OrderCode);
                }
                catch (Exception)
                {
                    await _payos.CancelPaymentLinkAsync(order.OrderCode);
                    
                    // 2. Cáº­p nháº­t Database
                    payment.Status = PaymentStatus.Cancelled;
                    payment.UpdatedAt = DateTime.UtcNow;
                }

                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                // Only an unpaid reservation is returned to the quota pool.
                // Paid/consumed promotions remain historical usage after cancellation.
                if (payment.Status == PaymentStatus.Cancelled)
                    PromotionUsageLifecycle.ReleaseReserved(
                        order.PromotionUsages,
                        order.UpdatedAt);

                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();
                await _orderRepo.CommitTransactionAsync();

                return ApiResponse<bool>.SuccessResponse(true, "Há»§y Ä‘Æ¡n hÃ ng thÃ nh cÃ´ng");
            }
            catch (Exception ex)
            {
                await _orderRepo.RollbackTransactionAsync();
                _logger.LogError(ex, $"Lá»—i xáº£y ra khi cáº­p nháº­t DB há»§y Ä‘Æ¡n hÃ ng #{orderCode}");
                return ApiResponse<bool>.Failure($"Lá»—i há»‡ thá»‘ng khi cáº­p nháº­t tráº¡ng thÃ¡i há»§y Ä‘Æ¡n. Lá»—i: {ex.Message}", data: false);
            }
        }

        //Chá»§ quÃ¡n há»§y Ä‘Æ¡n hÃ ng (Chá»‰ Ã¡p dá»¥ng cho quáº§y, khÃ´ng Ã¡p dá»¥ng cho khÃ¡ch Ä‘áº·t qua App)
        //CÃ³ 2 trÆ°á»ng há»£p :
        //1) Náº¿u khÃ¡ch tráº£ tiá»n máº·t thÃ¬ quáº§y há»§y lÃ  xong,
        //2) Náº¿u khÃ¡ch tráº£ online thÃ¬ quáº§y há»§y pháº£i cháº¡y luá»“ng hoÃ n tiá»n sang PayOS
#if false // Replaced by the recoverable implementation below.
        public async Task<ApiResponse<bool>> CancelOrderByBoothOwnerLegacyAsync(Guid boothOwnerId, long orderCode, RefundQRRequest request)
        {
            // 1. Kiá»ƒm tra request há»£p lá»‡ ngay tá»« Ä‘áº§u
            if (request == null) return ApiResponse<bool>.Failure("Dá»¯ liá»‡u yÃªu cáº§u khÃ´ng há»£p lá»‡.", data: false);

            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order is not null && order.BoothOwnerId != boothOwnerId)
                throw AppException.Forbidden("You cannot cancel an order owned by another booth.", "ORDER_ACCESS_DENIED");
            if (order == null) return ApiResponse<bool>.Failure("ÄÆ¡n hÃ ng khÃ´ng tá»“n táº¡i", data: false);

            // Chá»§ quÃ¡n KHÃ”NG Ä‘Æ°á»£c há»§y Ä‘Æ¡n Ä‘Ã£ hoÃ n thÃ nh hoáº·c Ä‘Ã£ há»§y
            if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                return ApiResponse<bool>.Failure("ÄÆ¡n hÃ ng Ä‘Ã£ hoÃ n táº¥t hoáº·c Ä‘Ã£ Ä‘Æ°á»£c há»§y trÆ°á»›c Ä‘Ã³.", data: false);
            }

            // TÃ¬m báº£n ghi thanh toÃ¡n thÃ nh cÃ´ng (náº¿u cÃ³)
            var paidPayment = order.Payments
                                   .OrderByDescending(p => p.CreatedAt)
                                   .FirstOrDefault(p => p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.Underpaid);

            string notificationTitle;
            string notificationContent;
            var requiresExternalRefund = paidPayment is not null
                && paidPayment.Gateway != PaymentGateway.None
                && paidPayment.Amount > 0m;

            // LUá»’NG 1: ÄÆ N HÃ€NG ÄÃƒ THANH TOÃN ONLINE -> KHá»žI Táº O HOÃ€N TIá»€N
            if (paidPayment is { Gateway: PaymentGateway.None, Amount: 0m })
            {
                paidPayment.Status = PaymentStatus.Cancelled;
                paidPayment.RefundReason = request.RefundReason;
                paidPayment.UpdatedAt = DateTime.UtcNow;
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = paidPayment.UpdatedAt;

                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();

                notificationTitle = "Fully discounted order cancelled";
                notificationContent = $"Order #{order.OrderCode} was cancelled. No refund is required.";
            }
            else if (paidPayment != null)
            {
                if (string.IsNullOrEmpty(request.AccountNumber) || string.IsNullOrEmpty(request.BankBin))
                {
                    return ApiResponse<bool>.Failure("ÄÆ¡n hÃ ng Ä‘Ã£ thanh toÃ¡n. Vui lÃ²ng cung cáº¥p Ä‘áº§y Ä‘á»§ Sá»‘ tÃ i khoáº£n vÃ  MÃ£ ngÃ¢n hÃ ng Ä‘á»ƒ hoÃ n tiá»n.", data: false);
                }

                try
                {
                    long refundAmount = paidPayment.Status == PaymentStatus.Underpaid
                            ? (long)paidPayment.Amount //Sá»‘ tiá»n thá»±c táº¿ khÃ¡ch Ä‘Ã£ tráº£ (trÆ°á»ng há»£p thanh toÃ¡n thiáº¿u)
                            : (long)order.FinalAmount; //Sá»‘ tiá»n cáº§n hoÃ n láº¡i cho khÃ¡ch (thanh toÃ¡n Ä‘áº§y Ä‘á»§)

                    var referenceId = $"refund_{order.OrderCode}_{DateTime.UtcNow.Ticks}"; // ThÃªm Ticks Ä‘á»ƒ trÃ¡nh trÃ¹ng ID khi gá»i láº¡i náº¿u lá»—i
                    var payoutRequest = new PayoutBatchRequest
                    {
                        ReferenceId = referenceId,
                        Category = new List<string> { "refund" },
                        ValidateDestination = true,
                        Payouts = new List<PayoutBatchItem>
                        {
                            new PayoutBatchItem
                            {
                                ReferenceId = $"{referenceId}_item",
                                Amount = refundAmount,
                                Description = $"Refund #{order.OrderCode}",
                                ToBin = request.BankBin,
                                ToAccountNumber = request.AccountNumber
                            }
                        }
                    };

                    // Gá»i lá»‡nh Payout sang PayOS
                    var payoutResult = await _payOutClient.Payouts.Batch.CreateAsync(payoutRequest);
                    _logger.LogInformation($"YÃªu cáº§u Payout hoÃ n tiá»n Ä‘Ã£ Ä‘Æ°á»£c gá»­i lÃªn PayOS cho Ä‘Æ¡n #{order.OrderCode}. Payout ID: {payoutResult.Id}");

                    //if (payoutResult != null && (payoutResult. == "COMPLETED" || payoutResult.Status == "SUCCESS"))
                    //{
                    //    // Tiá»n Ä‘Ã£ sang ngay láº­p tá»©c -> Chuyá»ƒn tháº³ng sang Refunded!
                    //    paidPayment.Status = PaymentStatus.Refunded;
                    //}
                    //else
                    //{
                    //    // TrÆ°á»ng há»£p lá»‡nh Ä‘Ã£ ghi nháº­n nhÆ°ng bÃªn NgÃ¢n hÃ ng Ä‘ang giá»¯ láº¡i xá»­ lÃ½
                    //    paidPayment.Status = PaymentStatus.RefundProcessing;
                    //}

                    paidPayment.Status = PaymentStatus.RefundProcessing;

                    // CHÃš Ã: LÃºc nÃ y tiá»n chÆ°a vá» tÃ i khoáº£n khÃ¡ch ngay, tráº¡ng thÃ¡i Ä‘Ãºng pháº£i lÃ  RefundProcessing
                    //paidPayment.Status = PaymentStatus.RefundProcessing;
                    paidPayment.RefundReason = request.RefundReason;
                    paidPayment.UpdatedAt = DateTime.UtcNow;

                    // ÄÆ¡n hÃ ng váº­t lÃ½ thÃ¬ cÃ³ thá»ƒ chuyá»ƒn sang Cancelled ngay láº­p tá»©c Ä‘á»ƒ nhÃ  báº¿p giáº£i phÃ³ng Ä‘Æ¡n
                    order.Status = OrderStatus.Cancelled;
                    //order.Note = $"Chá»§ quÃ¡n há»§y Ä‘Æ¡n. LÃ½ do: {request.RefundReason}. Äang chá» PayOS xá»­ lÃ½ hoÃ n tiá»n.";
                    order.UpdatedAt = DateTime.UtcNow;

                    _orderRepo.Update(order);
                    await _orderRepo.SaveChangesAsync();

                    notificationTitle = "ÄÆ¡n hÃ ng Ä‘Ã£ bá»‹ há»§y & Äang hoÃ n tiá»n";
                    notificationContent = $"ÄÆ¡n hÃ ng #{order.OrderCode} Ä‘Ã£ bá»‹ há»§y. Lá»‡nh hoÃ n tiá»n {refundAmount:N0}Ä‘ Ä‘ang Ä‘Æ°á»£c xá»­ lÃ½ qua PayOS. LÃ½ do: {request.RefundReason}";

                    // Gá»­i thÃ´ng bÃ¡o cho khÃ¡ch hÃ ng
                    //var notificationPayload = new NotificationListItemResponse
                    //{
                    //    Id = Guid.NewGuid(),
                    //    BoothId = order.BoothOwnerId,
                    //    Type = "ORDER_CANCELLED",
                    //    Title = "ÄÆ¡n hÃ ng Ä‘Ã£ bá»‹ há»§y & Äang hoÃ n tiá»n",
                    //    Content = $"ÄÆ¡n hÃ ng #{order.OrderCode} Ä‘Ã£ bá»‹ há»§y. Lá»‡nh hoÃ n tiá»n {order.FinalAmount:N0}Ä‘ Ä‘ang Ä‘Æ°á»£c xá»­ lÃ½. LÃ½ do: {request.RefundReason}",
                    //    IsRead = false,
                    //    ReferenceType = "Order",
                    //    ReferenceId = order.Id,
                    //    CreatedAt = DateTime.UtcNow
                    //};
                    //await _notificationPublisher.PublishAsync(order.CustomerId, notificationPayload, unreadCount: 1);

                    //return ApiResponse<bool>.SuccessResponse(true, "Chá»§ quÃ¡n há»§y Ä‘Æ¡n thÃ nh cÃ´ng. Há»‡ thá»‘ng Ä‘ang tiáº¿n hÃ nh hoÃ n tiá»n qua PayOS.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lá»—i khi gá»i API hoÃ n tiá»n PayOS cho Ä‘Æ¡n #{order.OrderCode}");
                    _logger.LogError($"Data (náº¿u cÃ³): {ex.Data}");
                    _logger.LogError($"Full Exception details: {ex}");
                    return ApiResponse<bool>.Failure($"Gá»i lá»‡nh hoÃ n tiá»n sang PayOS tháº¥t báº¡i. Vui lÃ²ng kiá»ƒm tra láº¡i sá»‘ tÃ i khoáº£n khÃ¡ch hoáº·c sá»‘ dÆ° vÃ­ PayOS. Lá»—i: {ex.Message}", data: false);
                }
            }
            else
            {
                // LUá»’NG 2: ÄÆ N HÃ€NG CHÆ¯A THANH TOÃN (CASH HOáº¶C PAYOS CHÆ¯A QUÃ‰T MÃƒ)
                // Cáº­p nháº­t táº¥t cáº£ cÃ¡c báº£n ghi thanh toÃ¡n chÆ°a thÃ nh cÃ´ng thÃ nh Cancelled
                var unPaidPayments = order.Payments.Where(p => p.Status == PaymentStatus.Pending).ToList();
                foreach (var p in unPaidPayments)
                {
                    p.Status = PaymentStatus.Cancelled;
                    p.RefundReason = request.RefundReason;
                    p.UpdatedAt = DateTime.UtcNow;
                }

                order.Status = OrderStatus.Cancelled;
                //order.Note = $"Chá»§ quÃ¡n há»§y Ä‘Æ¡n chÆ°a thanh toÃ¡n. LÃ½ do: {request.RefundReason}";
                order.UpdatedAt = DateTime.UtcNow;

                PromotionUsageLifecycle.ReleaseReserved(
                    order.PromotionUsages,
                    order.UpdatedAt);

                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();

                notificationTitle = "ÄÆ¡n hÃ ng Ä‘Ã£ bá»‹ há»§y";
                notificationContent = $"ÄÆ¡n hÃ ng #{order.OrderCode} Ä‘Ã£ bá»‹ há»§y bá»Ÿi chá»§ quÃ¡n. LÃ½ do: {request.RefundReason}";
            }

            // Gá»­i thÃ´ng bÃ¡o cho khÃ¡ch hÃ ng
            await PublishPersistedNotificationAsync(
                order.CustomerId,
                requiresExternalRefund
                    ? NotificationType.RefundPending
                    : NotificationType.OrderCancelled,
                notificationTitle,
                notificationContent,
                order.Id);

            return ApiResponse<bool>.SuccessResponse(true, requiresExternalRefund
                ? "Chá»§ quÃ¡n há»§y Ä‘Æ¡n thÃ nh cÃ´ng. Há»‡ thá»‘ng Ä‘ang tiáº¿n hÃ nh hoÃ n tiá»n qua PayOS."
                : "ÄÆ¡n hÃ ng Ä‘Ã£ Ä‘Æ°á»£c há»§y thÃ nh cÃ´ng.");
        }


        //TÃ¬nh huá»‘ng khÃ¡ch Ä‘áº·t mÃ³n payos, tráº£ tiá»n, nhÆ°ng máº¡ng lá»—i vÃ  webhook khÃ´ng vá» ká»‹p, khÃ¡ch báº¥m nÃºt "TÃ´i Ä‘Ã£ thanh toÃ¡n" trÃªn FE Ä‘á»ƒ xÃ¡c nháº­n, thÃ¬ gá»i API nÃ y Ä‘á»ƒ kiá»ƒm tra tráº¡ng thÃ¡i thá»±c táº¿ tá»« PayOS
#endif
        public async Task<ApiResponse<bool>> CancelOrderByBoothOwnerAsync(
            Guid boothOwnerId,
            long orderCode,
            RefundQRRequest request)
        {
            if (request is null)
                return ApiResponse<bool>.Failure("Refund request is required.", "REFUND_REQUEST_REQUIRED", false);

            Order? order = null;
            Payment? refundPayment = null;
            var transactionOpen = false;
            var needsPayout = false;
            var newlyCancelled = false;

            try
            {
                await _orderRepo.BeginTransactionAsync();
                transactionOpen = true;
                order = await _orderRepo.GetOrderByCodeForUpdateAsync(orderCode);

                if (order is null)
                    return await RollbackFailureAsync("Order was not found.", "ORDER_NOT_FOUND");
                if (order.BoothOwnerId != boothOwnerId)
                    throw AppException.Forbidden("You cannot cancel an order owned by another booth.", "ORDER_ACCESS_DENIED");

                refundPayment = order.Payments
                    .OrderByDescending(payment => payment.CreatedAt)
                    .FirstOrDefault(payment => payment.Status is PaymentStatus.RefundProcessing or PaymentStatus.Refunded);

                if (refundPayment?.Status == PaymentStatus.Refunded)
                {
                    await _orderRepo.CommitTransactionAsync();
                    transactionOpen = false;
                    return ApiResponse<bool>.SuccessResponse(true, "Refund was already completed.");
                }

                if (order.Status == OrderStatus.Completed)
                    return await RollbackFailureAsync("Completed orders cannot be cancelled.", "REFUND_NOT_ALLOWED");

                if (refundPayment is not null)
                {
                    await _orderRepo.CommitTransactionAsync();
                    transactionOpen = false;
                    return await DispatchOrReconcileRefundAsync(refundPayment, orderCode, request);
                }

                if (order.Status == OrderStatus.Cancelled)
                    return await RollbackFailureAsync("Order was already cancelled without a pending refund.", "REFUND_NOT_ALLOWED");

                var paidPayment = order.Payments
                    .OrderByDescending(payment => payment.CreatedAt)
                    .FirstOrDefault(payment => payment.Status is PaymentStatus.Paid or PaymentStatus.Underpaid);
                var now = DateTime.UtcNow;

                needsPayout = paidPayment is not null
                    && paidPayment.Gateway == PaymentGateway.Payos
                    && paidPayment.Amount > 0m;

                if (needsPayout)
                {
                    if (string.IsNullOrWhiteSpace(request.BankBin)
                        || string.IsNullOrWhiteSpace(request.AccountNumber))
                        return await RollbackFailureAsync(
                            "Bank BIN and account number are required for an online refund.",
                            "REFUND_DESTINATION_REQUIRED");
                    if (paidPayment!.Amount != decimal.Truncate(paidPayment.Amount)
                        || paidPayment.Amount > long.MaxValue)
                        return await RollbackFailureAsync("Refund amount is invalid for VND payout.", "REFUND_AMOUNT_INVALID");

                    paidPayment.Status = PaymentStatus.RefundProcessing;
                    paidPayment.RefundAmount = paidPayment.Amount;
                    paidPayment.RefundReference = $"refund-{paidPayment.Id:N}";
                    paidPayment.RefundReason = request.RefundReason;
                    paidPayment.RefundRequestedAt = now;
                    paidPayment.UpdatedAt = now;
                    refundPayment = paidPayment;
                }
                else if (paidPayment is not null)
                {
                    // Cash and fully-discounted orders never enter PayOS payout.
                    paidPayment.Status = PaymentStatus.Cancelled;
                    paidPayment.RefundReason = request.RefundReason;
                    paidPayment.UpdatedAt = now;
                }
                else
                {
                    foreach (var payment in order.Payments.Where(payment => payment.Status == PaymentStatus.Pending))
                    {
                        payment.Status = PaymentStatus.Cancelled;
                        payment.RefundReason = request.RefundReason;
                        payment.UpdatedAt = now;
                    }

                    PromotionUsageLifecycle.ReleaseReserved(order.PromotionUsages, now);
                }

                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = now;
                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();
                await _orderRepo.CommitTransactionAsync();
                transactionOpen = false;
                newlyCancelled = true;
            }
            catch
            {
                if (transactionOpen)
                    await _orderRepo.RollbackTransactionAsync();
                throw;
            }

            if (newlyCancelled && order is not null)
                await TryPublishCancellationAsync(order, request.RefundReason, needsPayout);

            if (!needsPayout || refundPayment is null)
                return ApiResponse<bool>.SuccessResponse(true, "Order was cancelled successfully.");

            return await DispatchOrReconcileRefundAsync(refundPayment, orderCode, request);

            async Task<ApiResponse<bool>> RollbackFailureAsync(string message, string code)
            {
                await _orderRepo.RollbackTransactionAsync();
                transactionOpen = false;
                return ApiResponse<bool>.Failure(message, code, false);
            }
        }

        public async Task<ApiResponse<bool>> ReconcileRefundAsync(Guid boothOwnerId, long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order is null)
                return ApiResponse<bool>.Failure("Order was not found.", "ORDER_NOT_FOUND", false);
            if (order.BoothOwnerId != boothOwnerId)
                throw AppException.Forbidden("You cannot access another booth's refund.", "ORDER_ACCESS_DENIED");

            var payment = order.Payments
                .OrderByDescending(candidate => candidate.CreatedAt)
                .FirstOrDefault(candidate => candidate.Status is PaymentStatus.RefundProcessing or PaymentStatus.Refunded);
            if (payment is null)
                return ApiResponse<bool>.Failure("Order has no refund to reconcile.", "REFUND_NOT_ALLOWED", false);
            if (payment.Status == PaymentStatus.Refunded)
                return ApiResponse<bool>.SuccessResponse(true, "Refund was already completed.");

            return await DispatchOrReconcileRefundAsync(payment, orderCode, request: null);
        }

        private async Task<ApiResponse<bool>> DispatchOrReconcileRefundAsync(
            Payment payment,
            long orderCode,
            RefundQRRequest? request)
        {
            if (payment.RefundAmount is null || string.IsNullOrWhiteSpace(payment.RefundReference))
                return ApiResponse<bool>.Failure(
                    "Refund record is incomplete and requires manual review.",
                    "PAYOUT_RECONCILIATION_REQUIRED",
                    false);

            try
            {
                PayOSPayoutSnapshot? snapshot;
                if (!string.IsNullOrWhiteSpace(payment.PayoutId))
                {
                    snapshot = await _payouts.GetAsync(payment.PayoutId);
                    return await ApplyPayoutSnapshotAsync(orderCode, payment.Id, snapshot, allowCompletion: true);
                }

                // Always query first. This closes the timeout gap where PayOS accepted
                // the previous request but the application did not receive its response.
                snapshot = await _payouts.FindByReferenceAsync(payment.RefundReference);
                if (snapshot is not null)
                    return await ApplyPayoutSnapshotAsync(orderCode, payment.Id, snapshot, allowCompletion: true);

                if (request is null
                    || string.IsNullOrWhiteSpace(request.BankBin)
                    || string.IsNullOrWhiteSpace(request.AccountNumber))
                    return ApiResponse<bool>.Failure(
                        "No provider payout was found. Resubmit the cancellation with the refund destination.",
                        "PAYOUT_RECONCILIATION_REQUIRED",
                        false);

                var claimTime = DateTime.UtcNow;
                var claimed = await _orderRepo.TryClaimPayoutCreationAsync(
                    payment.Id,
                    claimTime,
                    claimTime.AddMinutes(-5));
                if (claimed == 0)
                    return ApiResponse<bool>.Failure(
                        "Another worker is dispatching this payout. Reconcile before retrying.",
                        "REFUND_ALREADY_PROCESSING",
                        false);

                snapshot = await _payouts.CreateAsync(new PayOSPayoutCommand(
                    payment.RefundReference,
                    payment.Id.ToString("N"),
                    decimal.ToInt64(payment.RefundAmount.Value),
                    $"Refund order {orderCode}",
                    request.BankBin.Trim(),
                    request.AccountNumber.Trim()));

                // Creation only proves that the provider accepted the command. A later
                // signed status query is required before RefundProcessing -> Refunded.
                await ApplyPayoutSnapshotAsync(orderCode, payment.Id, snapshot, allowCompletion: false);
                return ApiResponse<bool>.SuccessResponse(true, "Order was cancelled and the refund is processing.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayOS payout outcome is unknown for order {OrderCode}; reconciliation is required.", orderCode);
                return ApiResponse<bool>.Failure(
                    "Order is cancelled, but the payout outcome is unknown. Reconcile before retrying.",
                    "PAYOUT_RECONCILIATION_REQUIRED",
                    false);
            }
        }

        private async Task<ApiResponse<bool>> ApplyPayoutSnapshotAsync(
            long orderCode,
            Guid paymentId,
            PayOSPayoutSnapshot snapshot,
            bool allowCompletion)
        {
            var transactionOpen = false;
            try
            {
                await _orderRepo.BeginTransactionAsync();
                transactionOpen = true;
                var order = await _orderRepo.GetOrderByCodeForUpdateAsync(orderCode);
                var payment = order?.Payments.FirstOrDefault(candidate => candidate.Id == paymentId);
                if (payment is null)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return ApiResponse<bool>.Failure("Refund payment was not found.", "REFUND_NOT_FOUND", false);
                }

                if (!string.Equals(payment.RefundReference, snapshot.ReferenceId, StringComparison.Ordinal)
                    || payment.RefundAmount != snapshot.Amount)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return ApiResponse<bool>.Failure(
                        "Provider payout does not match the authoritative refund.",
                        "PAYOUT_DATA_MISMATCH",
                        false);
                }

                payment.PayoutId ??= snapshot.PayoutId;
                payment.UpdatedAt = DateTime.UtcNow;
                var refundCompleted = payment.Status != PaymentStatus.Refunded
                    && allowCompletion
                    && snapshot.Outcome == PayOSPayoutOutcome.Succeeded;
                if (allowCompletion && snapshot.Outcome == PayOSPayoutOutcome.Succeeded)
                {
                    payment.Status = PaymentStatus.Refunded;
                    payment.RefundedAt = payment.UpdatedAt;
                }

                await _orderRepo.SaveChangesAsync();
                await _orderRepo.CommitTransactionAsync();
                transactionOpen = false;

                if (refundCompleted)
                {
                    await PublishPersistedNotificationAsync(
                        order!.CustomerId,
                        NotificationType.RefundCompleted,
                        "Refund completed",
                        $"The refund for order #{order.OrderCode} has been completed.",
                        order.Id);
                }

                if (snapshot.Outcome == PayOSPayoutOutcome.Failed)
                    return ApiResponse<bool>.Failure(
                        "PayOS reports that the payout failed; manual review is required.",
                        "PAYOUT_PROVIDER_REJECTED",
                        false);
                return ApiResponse<bool>.SuccessResponse(
                    true,
                    payment.Status == PaymentStatus.Refunded
                        ? "Refund completed."
                        : "Refund is still processing.");
            }
            catch
            {
                if (transactionOpen)
                    await _orderRepo.RollbackTransactionAsync();
                throw;
            }
        }

        private async Task TryPublishCancellationAsync(Order order, string? reason, bool refundProcessing)
        {
            try
            {
                await PublishPersistedNotificationAsync(
                    order.CustomerId,
                    refundProcessing
                        ? NotificationType.RefundPending
                        : NotificationType.OrderCancelled,
                    refundProcessing ? "Order cancelled - refund processing" : "Order cancelled",
                    $"Order #{order.OrderCode} was cancelled. Reason: {reason}",
                    order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Order {OrderCode} was cancelled but its notification could not be published.", order.OrderCode);
            }
        }

        public async Task<ApiResponse<bool>> ActiveCheckPaymentStatus(long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order == null) return ApiResponse<bool>.Failure("ÄÆ¡n hÃ ng khÃ´ng tá»“n táº¡i", data: false);

            // Náº¿u Ä‘Æ¡n Ä‘Ã£ xá»­ lÃ½ thÃ nh cÃ´ng trÆ°á»›c Ä‘Ã³ rá»“i thÃ¬ thÃ´i
            if (order.Status != OrderStatus.Placed && 
                order.Status != OrderStatus.Underpaid && 
                order.Status != OrderStatus.Cancelled) //order cÃ³ status tá»« preparing, ready, completed thÃ¬ coi nhÆ° Ä‘Ã£ thanh toÃ¡n thÃ nh cÃ´ng rá»“i
                return ApiResponse<bool>.SuccessResponse(true, "ÄÆ¡n Ä‘Ã£ Ä‘Æ°á»£c thanh toÃ¡n vÃ  Ä‘ang xá»­ lÃ½.");

            try
            {
                // 1. CHá»¦ Äá»˜NG Gá»ŒI SANG PAYOS Äá»‚ KIá»‚M TRA (KhÃ´ng Ä‘á»£i Webhook)
                var paymentInfo = await _payos.GetPaymentStatusAsync(orderCode);
                if (paymentInfo is null)
                    return ApiResponse<bool>.Failure(
                        "PayOS payment status is unavailable.",
                        "PAYOS_STATUS_UNAVAILABLE",
                        false);

                // 2. If PayOS reports payment received (PAID)
                if (paymentInfo.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                {
                    if (order.Status == OrderStatus.Cancelled)
                    {
                        var latePayment = order.Payments
                            .OrderByDescending(payment => payment.CreatedAt)
                            .FirstOrDefault();
                        if (latePayment is not null)
                        {
                            latePayment.Status = PaymentStatus.RefundProcessing;
                            // Preserve the original payment intent. The provider-confirmed
                            // received amount is snapshotted separately for refund.
                            latePayment.RefundAmount = paymentInfo.AmountPaid;
                            latePayment.RefundReference = $"refund-{latePayment.Id:N}";
                            latePayment.GatewayRef = paymentInfo.FirstTransactionReference;
                            latePayment.RefundReason = "Payment confirmed after cancellation.";
                            latePayment.RefundRequestedAt = DateTime.UtcNow;
                            latePayment.UpdatedAt = latePayment.RefundRequestedAt.Value;
                            await _orderRepo.SaveChangesAsync();
                        }

                        return ApiResponse<bool>.Failure(
                            "Payment arrived after cancellation and requires refund.",
                            "LATE_PAYMENT_REFUND_REQUIRED",
                            false);
                    }

                    if (paymentInfo.AmountPaid < (long)Math.Round(order.FinalAmount))
                    {
                        order.Status = OrderStatus.Underpaid;
                        order.UpdatedAt = DateTime.UtcNow;
                        _orderRepo.Update(order);
                        await _orderRepo.SaveChangesAsync();

                        await PublishPersistedNotificationAsync(
                            order.BoothOwnerId,
                            NotificationType.PaymentFailed,
                            "Order underpaid!",
                            $"Order {order.OrderCode} payment verification detected underpayment. Required: {order.FinalAmount:N0}d, Received: {paymentInfo.AmountPaid:N0}d",
                            order.Id);

                        return ApiResponse<bool>.Failure("You paid less than the required amount. Please contact the booth to resolve.", data: false);
                    }

                    var previousStatus = order.Status;

                    order.Status = OrderStatus.Preparing;
                    order.UpdatedAt = DateTime.UtcNow;

                    var payment = order.Payments
                                       .OrderByDescending(p => p.CreatedAt)
                                       .FirstOrDefault(p => p.Status == PaymentStatus.Pending);
                    if (payment != null)
                    {
                        payment.Status = PaymentStatus.Paid;
                        payment.GatewayRef = paymentInfo.FirstTransactionReference;
                        payment.PaidAt = DateTime.UtcNow;
                        payment.UpdatedAt = DateTime.UtcNow;
                    }

                    PromotionUsageLifecycle.ConsumeReserved(
                        order.PromotionUsages,
                        order.UpdatedAt);

                    _orderRepo.Update(order);
                    await _orderRepo.SaveChangesAsync();

                    // 4. Chuáº©n bá»‹ ná»™i dung thÃ´ng bÃ¡o SignalR
                    string notificationTitle = previousStatus == OrderStatus.Cancelled
                        ? "ÄÆ¡n Ä‘Ã£ há»§y Ä‘Æ°á»£c thanh toÃ¡n trá»…!"
                        : "ÄÆ¡n hÃ ng Ä‘Ã£ thanh toÃ¡n!";

                    string notificationContent = previousStatus == OrderStatus.Cancelled
                        ? $"ÄÆ¡n hÃ ng #{order.OrderCode} (tá»«ng bá»‹ há»§y do quÃ¡ háº¡n) vá»«a Ä‘Æ°á»£c Ä‘á»‘i soÃ¡t thanh toÃ¡n thÃ nh cÃ´ng qua PayOS. Sá»‘ tiá»n: {order.FinalAmount:N0}Ä‘"
                        : $"ÄÆ¡n hÃ ng #{order.OrderCode} Ä‘Ã£ Ä‘Æ°á»£c thanh toÃ¡n thÃ nh cÃ´ng qua PayOS. Sá»‘ tiá»n: {order.FinalAmount:N0}Ä‘";

                    await PublishPersistedNotificationAsync(
                        order.BoothOwnerId,
                        NotificationType.PaymentSucceeded,
                        notificationTitle,
                        notificationContent,
                        order.Id);

                    return ApiResponse<bool>.SuccessResponse(true, "Báº¡n Ä‘Ã£ thanh toÃ¡n thÃ nh cÃ´ng! Chá»§ quÃ¡n Ä‘ang chuáº©n bá»‹ Ä‘Æ¡n hÃ ng.");
                }

                return ApiResponse<bool>.Failure("Thanh toÃ¡n chÆ°a Ä‘Æ°á»£c ghi nháº­n trÃªn há»‡ thá»‘ng PayOS", data: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lá»—i xáº£y ra khi Ä‘á»‘i soÃ¡t Ä‘Æ¡n hÃ ng #{orderCode}");
                return ApiResponse<bool>.Failure("Lá»—i khi Ä‘á»‘i soÃ¡t vá»›i PayOS. Vui lÃ²ng thá»­ láº¡i sau.", "PAYOS_RECONCILIATION_FAILED", data: false);
            }
        }

        //public async Task<ApiResponse<bool>> RefundOrderAsync(long orderCode, 
        //                                                      string reason, 
        //                                                      string customerBankBin,  //MÃ£ BIN ngÃ¢n hÃ ng (6 sá»‘ Ä‘áº§u) cá»§a khÃ¡ch Ä‘á»ƒ PayOS Ä‘á»‘i chiáº¿u, náº¿u cÃ³
        //                                                      string customerAccountNumber) //Sá»‘ tÃ i khoáº£n ngÃ¢n hÃ ng cá»§a khÃ¡ch Ä‘á»ƒ PayOS Ä‘á»‘i chiáº¿u, náº¿u cÃ³
        //{
        //    // 1. TÃ¬m Ä‘Æ¡n hÃ ng kÃ¨m danh sÃ¡ch thanh toÃ¡n
        //    var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
        //    if (order == null) return ApiResponse<bool>.Failure("ÄÆ¡n hÃ ng khÃ´ng tá»“n táº¡i", data: false);

        //    // 2. Kiá»ƒm tra tráº¡ng thÃ¡i Ä‘Æ¡n hÃ ng: Chá»‰ cho hoÃ n tiá»n khi Ä‘Æ¡n Ä‘Ã£ thanh toÃ¡n thÃ nh cÃ´ng vÃ  quáº§y chÆ°a hoÃ n táº¥t phá»¥c vá»¥ mÃ³n (ChÆ°a giao hÃ ng)
        //    if (order.Status != OrderStatus.Preparing &&
        //        order.Status != OrderStatus.ReadyForPickup &&
        //        order.Status != OrderStatus.Underpaid) // khÃ¡ch tráº£ thiáº¿u cÅ©ng cáº§n hoÃ n
        //    {
        //        return ApiResponse<bool>.Failure($"ÄÆ¡n hÃ ng á»Ÿ tráº¡ng thÃ¡i '{order.Status}' khÃ´ng thá»a mÃ£n Ä‘iá»u kiá»‡n Ä‘á»ƒ hoÃ n tiá»n.", data: false);
        //    }

        //    // 3. TÃ¬m báº£n ghi Payment Ä‘Ã£ thanh toÃ¡n thÃ nh cÃ´ng (Paid) Ä‘á»ƒ hoÃ n láº¡i
        //    var paidPayment = order.Payments
        //                           .OrderByDescending(p => p.CreatedAt)
        //                           .FirstOrDefault(p => p.Status == PaymentStatus.Paid);

        //    if (paidPayment == null)
        //    {
        //        return ApiResponse<bool>.Failure("KhÃ´ng tÃ¬m tháº¥y giao dá»‹ch Ä‘Ã£ thanh toÃ¡n thÃ nh cÃ´ng cá»§a Ä‘Æ¡n hÃ ng nÃ y Ä‘á»ƒ thá»±c hiá»‡n hoÃ n tiá»n.", data: false);
        //    }

        //    try
        //    {
        //        // 4. LOGIC Xá»¬ LÃ HOÃ€N TIá»€N

        //        var referenceId = $"refund_{order.OrderCode}";

        //        var payoutRequest = new PayoutBatchRequest
        //        {
        //            ReferenceId = referenceId,
        //            Category = new List<string> { "refund" }, // Äá»•i category thÃ nh refund cho Ä‘Ãºng nghiá»‡p vá»¥
        //            ValidateDestination = true,               // YÃªu cáº§u PayOS check xem tÃ i khoáº£n Ä‘Ã­ch cÃ³ tháº­t khÃ´ng
        //            Payouts = new List<PayoutBatchItem>
        //            {
        //                new PayoutBatchItem
        //                {
        //                    ReferenceId = $"{referenceId}_item",
        //                    Amount = (long)order.FinalAmount, // Sá»‘ tiá»n hoÃ n báº±ng Ä‘Ãºng sá»‘ tiá»n Ä‘Æ¡n hÃ ng Ä‘Ã£ tráº£
        //                    Description = $"Hoan tien don hang #{order.OrderCode}", // Viáº¿t khÃ´ng dáº¥u Ä‘á»ƒ trÃ¡nh lá»—i font ngÃ¢n hÃ ng
        //                    ToBin = customerBankBin,           // Truyá»n mÃ£ BIN ngÃ¢n hÃ ng khÃ¡ch
        //                    ToAccountNumber = customerAccountNumber // Sá»‘ tÃ i khoáº£n khÃ¡ch
        //                }
        //            }
        //        };

        //        // Gá»i lá»‡nh Payout thá»±c sá»± sang PayOS
        //        var payoutResult = await _payOSClient.Payouts.Batch.CreateAsync(payoutRequest);
        //        _logger.LogInformation($"YÃªu cáº§u Payout hoÃ n tiá»n thÃ nh cÃ´ng cho Ä‘Æ¡n #{order.OrderCode}. Payout ID: {payoutResult.Id}");

        //        // Cáº­p nháº­t tráº¡ng thÃ¡i báº£ng thanh toÃ¡n con sang Refunded (ÄÃ£ hoÃ n tiá»n)
        //        paidPayment.Status = PaymentStatus.Refunded;
        //        paidPayment.UpdatedAt = DateTime.UtcNow;

        //        // Cáº­p nháº­t tráº¡ng thÃ¡i Ä‘Æ¡n hÃ ng cha sang Refunded
        //        order.Status = OrderStatus.Refunded;
        //        order.UpdatedAt = DateTime.UtcNow;

        //        _orderRepo.Update(order);
        //        await _orderRepo.SaveChangesAsync();

        //        // 5. Báº¯n thÃ´ng bÃ¡o SignalR xuá»‘ng Client (Cáº£ chá»§ quÃ¡n vÃ  khÃ¡ch hÃ ng Ä‘á»ƒ há» nháº­n thÃ´ng tin)
        //        var notificationPayload = new NotificationListItemResponse
        //        {
        //            Id = Guid.NewGuid(),
        //            BoothId = order.BoothOwnerId,
        //            Type = "ORDER_REFUNDED",
        //            Title = "ÄÆ¡n hÃ ng Ä‘Ã£ Ä‘Æ°á»£c hoÃ n tiá»n!",
        //            Content = $"ÄÆ¡n hÃ ng #{order.OrderCode} Ä‘Ã£ Ä‘Æ°á»£c hoÃ n tiá»n thÃ nh cÃ´ng. Sá»‘ tiá»n hoÃ n: {order.FinalAmount:N0}Ä‘. LÃ½ do: {reason}",
        //            IsRead = false,
        //            ReferenceType = "Order",
        //            ReferenceId = order.Id,
        //            CreatedAt = DateTime.UtcNow
        //        };

        //        // BÃ¡o cho chá»§ quáº§y qua SignalR
        //        await _notificationPublisher.PublishAsync(order.BoothOwnerId, notificationPayload, unreadCount: 1);

        //        return ApiResponse<bool>.SuccessResponse(true, "YÃªu cáº§u hoÃ n tiá»n Ä‘Ã£ Ä‘Æ°á»£c xá»­ lÃ½ vÃ  cáº­p nháº­t thÃ nh cÃ´ng.");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Lá»—i xáº£y ra khi thá»±c hiá»‡n hoÃ n tiá»n cho Ä‘Æ¡n hÃ ng #{orderCode}");
        //        return ApiResponse<bool>.Failure($"Lá»—i há»‡ thá»‘ng khi hoÃ n tiá»n: {ex.Message}", data: false);
        //    }
        //}

        private async Task TryCancelPayOSLinkAsync(long orderCode)
        {
            try
            {
                await _payos.CancelPaymentLinkAsync(orderCode);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to cancel orphan PayOS link {OrderCode}.",
                    orderCode);
            }
        }

        private async Task PublishPersistedNotificationAsync(
            Guid userId,
            NotificationType type,
            string title,
            string content,
            Guid orderId)
        {
            try
            {
                if (_notifications is not null)
                {
                    await _notifications.NotifyAsync(new NotificationMessage(
                        userId,
                        type,
                        title,
                        content,
                        null,
                        "Order",
                        orderId));
                    return;
                }

                // Compatibility fallback for isolated legacy tests/hosts that have not
                // registered the persisted notification service yet.
                await _notificationPublisher.PublishAsync(
                    userId,
                    new NotificationListItemResponse
                    {
                        Id = Guid.NewGuid(),
                        Type = type.ToString(),
                        Title = title,
                        Content = content,
                        IsRead = false,
                        ReferenceType = "Order",
                        ReferenceId = orderId,
                        CreatedAt = DateTime.UtcNow
                    },
                    1);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Order {OrderId} was committed but {NotificationType} notification delivery failed.",
                    orderId,
                    type);
            }
        }

        private async Task TryPublishOrderCreatedAsync(
            Order order,
            DateTime utcNow,
            bool isZeroPaymentOrder)
        {
            try
            {
                await PublishPersistedNotificationAsync(
                    order.BoothOwnerId,
                    NotificationType.OrderCreated,
                    isZeroPaymentOrder
                        ? "CÃ³ Ä‘Æ¡n hÃ ng Ä‘Æ°á»£c giáº£m 100%"
                        : "CÃ³ Ä‘Æ¡n hÃ ng tiá»n máº·t má»›i",
                    $"ÄÆ¡n #{order.OrderCode}, sá»‘ tiá»n {order.FinalAmount:N0}Ä‘.",
                    order.Id);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Order {OrderCode} committed but notification failed.",
                    order.OrderCode);
            }
        }

        private async Task TryPublishPaymentSucceededAsync(Order order, DateTime utcNow)
        {
            try
            {
                await PublishPersistedNotificationAsync(
                    order.BoothOwnerId,
                    NotificationType.PaymentSucceeded,
                    "ÄÆ¡n hÃ ng Ä‘Ã£ thanh toÃ¡n",
                    $"ÄÆ¡n #{order.OrderCode} Ä‘Ã£ thanh toÃ¡n {order.FinalAmount:N0}Ä‘.",
                    order.Id);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Payment committed for order {OrderCode} but notification failed.",
                    order.OrderCode);
            }
        }

        public async Task<ApiResponse<PaginationResp<BoothOwnerOrderListItemResponse>>> GetBoothOwnerOrdersAsync(
            Guid boothOwnerId,
            BoothOwnerOrderQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate > query.ToDate)
                throw AppException.BadRequest("The start date must be before the end date.", "INVALID_DATE_RANGE");

            _ = await _boothRepo.GetByOwnerIdAsync(boothOwnerId, cancellationToken)
                ?? throw AppException.NotFound("No booth is assigned to this account.", "BOOTH_NOT_FOUND");

            var toDate = query.ToDate;
            if (toDate.HasValue && toDate.Value.TimeOfDay == TimeSpan.Zero)
                toDate = toDate.Value.Date.AddDays(1).AddTicks(-1);

            var page = await _orderRepo.GetByBoothOwnerPagedAsync(
                boothOwnerId, query.Keyword, query.Status, query.PaymentStatus,
                query.FromDate, toDate, query.Page, query.PageSize, cancellationToken);
            var items = page.Items.Select(MapBoothOwnerOrderListItem).ToList();

            return ApiResponse<PaginationResp<BoothOwnerOrderListItemResponse>>.SuccessResponse(
                PaginationResp<BoothOwnerOrderListItemResponse>.Create(
                    items, page.TotalCount,
                    new PaginationReq { Page = query.Page, PageSize = query.PageSize }));
        }

        public async Task<ApiResponse<BoothOwnerOrderDetailResponse>> GetBoothOwnerOrderAsync(
            Guid boothOwnerId,
            long orderCode,
            CancellationToken cancellationToken = default)
        {
            var order = await _orderRepo.GetByBoothOwnerAndCodeAsync(
                boothOwnerId, orderCode, cancellationToken)
                ?? throw AppException.NotFound("Order not found.", "ORDER_NOT_FOUND");
            var payment = LatestPayment(order);

            var response = new BoothOwnerOrderDetailResponse
            {
                OrderCode = order.OrderCode,
                CustomerName = GetCustomerName(order),
                IsWalkInCustomer = IsWalkInCustomer(order.CustomerId),
                ItemCount = order.OrderDetails.Sum(detail => detail.Quantity),
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                FinalAmount = order.FinalAmount,
                PaymentMethod = payment?.Type,
                PaymentStatus = payment?.Status,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Note = order.Note,
                Items = order.OrderDetails.Select(detail => new BoothOwnerOrderItemResponse
                {
                    FoodItemName = detail.FoodItem.Name,
                    ImageUrl = detail.FoodItem.ThumbnailUrl,
                    Quantity = detail.Quantity,
                    UnitPrice = detail.UnitPrice,
                    TotalPrice = detail.TotalPrice
                }).ToList()
            };

            return ApiResponse<BoothOwnerOrderDetailResponse>.SuccessResponse(response);
        }

        public async Task<ApiResponse<OrderResponseDto>> CreateWalkInOrderAsync(
            Guid boothOwnerId,
            CreateWalkInOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Items.Count == 0)
                throw AppException.BadRequest("Add at least one item to the order.", "ORDER_ITEMS_REQUIRED");

            if (!Enum.IsDefined(request.PaymentMethod))
                throw AppException.BadRequest("The selected payment method is invalid.", "INVALID_PAYMENT_METHOD");

            var booth = await _boothRepo.GetByOwnerIdAsync(boothOwnerId, cancellationToken)
                ?? throw AppException.NotFound("No booth is assigned to this account.", "BOOTH_NOT_FOUND");

            if (booth.Status != BoothStatus.Active)
                throw AppException.Conflict("This booth is not currently active.", "BOOTH_NOT_ACTIVE");
            if (booth.NightMarket is null || booth.NightMarket.IsDeleted)
                throw AppException.Conflict("This night market is no longer available.", "MARKET_UNAVAILABLE");

            var normalizedItems = request.Items
                .GroupBy(item => item.FoodItemId)
                .Select(group => new CreateWalkInOrderItemRequest
                {
                    FoodItemId = group.Key,
                    Quantity = group.Sum(item => item.Quantity)
                })
                .ToList();

            if (normalizedItems.Any(item => item.FoodItemId == Guid.Empty || item.Quantity is < 1 or > 100))
                throw AppException.BadRequest("One or more order items are invalid.", "INVALID_ORDER_ITEM");

            var foodIds = normalizedItems.Select(item => item.FoodItemId).ToList();
            var foods = await _foodItemRepo.GetActiveByIdsAndBoothAsync(
                booth.Id, foodIds, cancellationToken);
            if (foods.Count != foodIds.Count)
                throw AppException.Conflict(
                    "One or more items are unavailable or do not belong to this booth.",
                    "ORDER_ITEM_UNAVAILABLE");

            return await CreateOrderAsync(new CreateOrderDto
            {
                CustomerId = null,
                BoothOwnerId = boothOwnerId,
                BoothId = booth.Id,
                Note = request.Note?.Trim(),
                PaymentMethod = request.PaymentMethod,
                PromotionCode = null,
                IsCreatedByBooth = true,
                Items = normalizedItems.Select(item => new CartItemDto
                {
                    FoodItemId = item.FoodItemId,
                    Quantity = item.Quantity,
                    UnitPrice = 0
                }).ToList()
            });
        }

        public async Task<ApiResponse<bool>> UpdateBoothOwnerOrderStatusAsync(
            Guid boothOwnerId,
            long orderCode,
            UpdateBoothOwnerOrderStatusRequest request,
            CancellationToken cancellationToken = default)
        {
            var order = await _orderRepo.GetByBoothOwnerAndCodeAsync(
                boothOwnerId, orderCode, cancellationToken)
                ?? throw AppException.NotFound("Order not found.", "ORDER_NOT_FOUND");

            if (!IsValidTransition(order.Status, request.NewStatus))
                throw AppException.Conflict(
                    $"The order cannot move from {order.Status} to {request.NewStatus}.",
                    "INVALID_ORDER_STATUS_TRANSITION");
            if (request.NewStatus == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(request.Reason))
                throw AppException.BadRequest("A cancellation reason is required.", "CANCELLATION_REASON_REQUIRED");

            var payment = LatestPayment(order);
            if (request.NewStatus == OrderStatus.Preparing
                && payment?.Type == PaymentType.PayOS
                && payment.Status != PaymentStatus.Paid)
                throw AppException.Conflict(
                    "Online payment must be confirmed before preparation starts.", "PAYMENT_REQUIRED");
            if (request.NewStatus == OrderStatus.Completed
                && !order.Payments.Any(item => item.Status == PaymentStatus.Paid))
                throw AppException.Conflict(
                    "Payment must be confirmed before completing the order.", "PAYMENT_REQUIRED");

            if (request.NewStatus == OrderStatus.Cancelled)
            {
                order.Note = string.IsNullOrWhiteSpace(order.Note)
                    ? $"Cancellation reason: {request.Reason!.Trim()}"
                    : $"{order.Note}\nCancellation reason: {request.Reason!.Trim()}";
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;
                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();
            }
            else
            {
                var affected = await _orderRepo.UpdateBoothOwnerOrderStatusAsync(
                    boothOwnerId, orderCode, order.Status, request.NewStatus,
                    DateTime.UtcNow, cancellationToken);
                if (affected == 0)
                    throw AppException.Conflict(
                        "The order was updated by another request. Refresh and try again.",
                        "ORDER_STATUS_CONFLICT");
            }

            await PublishOrderStatusAsync(order, request.NewStatus);
            return ApiResponse<bool>.SuccessResponse(true, "Order status updated successfully.");
        }

        public async Task<ApiResponse<bool>> ConfirmCashPaymentAsync(
            Guid boothOwnerId,
            long orderCode,
            CancellationToken cancellationToken = default)
        {
            var order = await _orderRepo.GetByBoothOwnerAndCodeAsync(
                boothOwnerId, orderCode, cancellationToken)
                ?? throw AppException.NotFound("Order not found.", "ORDER_NOT_FOUND");
            var payment = LatestPayment(order)
                ?? throw AppException.NotFound("Payment record not found.", "PAYMENT_NOT_FOUND");

            if (payment.Type != PaymentType.Cash)
                throw AppException.Conflict(
                    "Only cash payments can be confirmed manually.", "INVALID_PAYMENT_METHOD");
            if (payment.Status == PaymentStatus.Paid)
                throw AppException.Conflict(
                    "This cash payment has already been confirmed.", "PAYMENT_ALREADY_CONFIRMED");
            if (order.Status is OrderStatus.Cancelled or OrderStatus.Refunded)
                throw AppException.Conflict(
                    "Payment cannot be confirmed for this order.", "ORDER_NOT_PAYABLE");

            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;
            await _orderRepo.SaveChangesAsync();
            return ApiResponse<bool>.SuccessResponse(true, "Cash payment confirmed successfully.");
        }

        private BoothOwnerOrderListItemResponse MapBoothOwnerOrderListItem(Order order)
        {
            var payment = LatestPayment(order);
            return new BoothOwnerOrderListItemResponse
            {
                OrderCode = order.OrderCode,
                CustomerName = GetCustomerName(order),
                IsWalkInCustomer = IsWalkInCustomer(order.CustomerId),
                ItemCount = order.OrderDetails.Sum(detail => detail.Quantity),
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                FinalAmount = order.FinalAmount,
                PaymentMethod = payment?.Type,
                PaymentStatus = payment?.Status,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            };
        }

        private static Payment? LatestPayment(Order order)
            => order.Payments.OrderByDescending(payment => payment.CreatedAt).FirstOrDefault();

        private bool IsWalkInCustomer(Guid customerId)
        {
            var configured = _config["SystemSettings:WalkInCustomerId"]
                ?? "00000000-0000-0000-0000-000000000001";
            return Guid.TryParse(configured, out var walkInId) && customerId == walkInId;
        }

        private string GetCustomerName(Order order)
            => IsWalkInCustomer(order.CustomerId)
                ? "Walk-in customer"
                : order.Customer?.FullName ?? "Customer information unavailable";

        private static bool IsValidTransition(OrderStatus current, OrderStatus next)
            => (current, next) switch
            {
                (OrderStatus.Placed, OrderStatus.Preparing) => true,
                (OrderStatus.Placed, OrderStatus.Cancelled) => true,
                (OrderStatus.Preparing, OrderStatus.ReadyForPickup) => true,
                (OrderStatus.ReadyForPickup, OrderStatus.Completed) => true,
                _ => false
            };

        private async Task PublishOrderStatusAsync(Order order, OrderStatus newStatus)
        {
            if (IsWalkInCustomer(order.CustomerId))
                return;

            var (title, content) = newStatus switch
            {
                OrderStatus.Preparing => ("Your order is being prepared", $"Order #{order.OrderCode} is now being prepared."),
                OrderStatus.ReadyForPickup => ("Your order is ready", $"Order #{order.OrderCode} is ready for pickup."),
                OrderStatus.Completed => ("Order completed", $"Order #{order.OrderCode} has been completed."),
                OrderStatus.Cancelled => ("Order cancelled", $"Order #{order.OrderCode} has been cancelled."),
                _ => (string.Empty, string.Empty)
            };
            if (string.IsNullOrEmpty(title))
                return;

            try
            {
                await _notificationPublisher.PublishAsync(
                    order.CustomerId,
                    new NotificationListItemResponse
                    {
                        Id = Guid.NewGuid(),
                        BoothId = order.BoothOwnerId,
                        Type = $"ORDER_{newStatus.ToString().ToUpperInvariant()}",
                        Title = title,
                        Content = content,
                        IsRead = false,
                        ReferenceType = "Order",
                        ReferenceId = order.Id,
                        CreatedAt = DateTime.UtcNow
                    },
                    unreadCount: 1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Order {OrderCode} status changed to {OrderStatus}, but its realtime notification could not be delivered.",
                    order.OrderCode,
                    newStatus);
            }
        }

        public async Task<bool> HasOrderWithCodeAsync(long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            return order != null;
        }

    }
}
