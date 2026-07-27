using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.Promotions;
using ApplicationLayer.Services.CustomerDiscovery;
using ApplicationLayer.Services.PayOS;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        //DÃ nh cho customer láº«n khÃ¡ch vang lai (Walk-in) Ä‘áº·t mÃ³n, tráº£ vá» link thanh toÃ¡n náº¿u chá»n online

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
        //    // 1. TÃ¬m Ä‘Æ¡n hÃ ng cáº§n há»§y trong Database
        //    var order = await _orderRepo.GetByIdAsync(dto.OrderId);
        //    if (order == null) throw new Exception("KhÃ´ng tÃ¬m tháº¥y Ä‘Æ¡n hÃ ng!");

        //    // 2. Kiá»ƒm tra tráº¡ng thÃ¡i: Chá»‰ Ä‘Æ°á»£c tá»« chá»‘i khi Ä‘Æ¡n hÃ ng má»›i Ä‘áº·t (Placed hoáº·c PendingPayment)
        //    if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
        //    {
        //        throw new Exception("ÄÆ¡n hÃ ng Ä‘Ã£ hoÃ n thÃ nh hoáº·c Ä‘Ã£ bá»‹ há»§y trÆ°á»›c Ä‘Ã³, khÃ´ng thá»ƒ tá»« chá»‘i!");
        //    }

        //    // 3. Xá»¬ LÃ Ráº¼ NHÃNH DÃ’NG TIá»€N:

        //    // TRÆ¯á»œNG Há»¢P 1: KhÃ¡ch Ä‘áº·t báº±ng TIá»€N Máº¶T
        //    if (order.PayStatus == PayOrderStatus.Pending)
        //    {
        //        order.Status = OrderStatus.Cancelled;
        //        order.Note = dto.Reason; // LÆ°u váº¿t lÃ½ do há»§y
        //        order.UpdatedAt = DateTime.UtcNow;
        //        order.PayStatus = PayOrderStatus.Failed;

        //        // Cáº­p nháº­t báº£ng Payment sang tráº¡ng thÃ¡i tháº¥t báº¡i/há»§y
        //        var payment = order.Payments.FirstOrDefault(p => p.Status == PaymentStatus.Pending);
        //        if (payment != null) payment.Status = PaymentStatus.Failed;
        //    }
        //    // TRÆ¯á»œNG Há»¢P 2: KhÃ¡ch Ä‘áº·t ONLINE vÃ  tráº¡ng thÃ¡i Ä‘Ã£ bÃ¡o ÄÃƒ THANH TOÃN (Paid = 1)
        //    else if (order.PayStatus == PayOrderStatus.Paid)
        //    {
        //        // BÆ°á»›c A: Äá»•i tráº¡ng thÃ¡i Ä‘Æ¡n hÃ ng sang "Äang chá» hoÃ n tiá»n"
        //        order.Status = OrderStatus.Cancelled;
        //        order.PayStatus = PayOrderStatus.RefundPending;
        //        order.Note = dto.Reason;
        //        order.UpdatedAt = DateTime.UtcNow;

        //        // TÃ¬m báº£n ghi lá»‹ch sá»­ giao dá»‹ch thÃ nh cÃ´ng trÆ°á»›c Ä‘Ã³ Ä‘á»ƒ láº¥y thÃ´ng tin Ä‘á»‘i chiáº¿u
        //        var successPayment = order.Payments.FirstOrDefault(p => p.Status == PaymentStatus.Paid);

        //        // BÆ°á»›c B: Gá»i API kÃ­ch hoáº¡t lá»‡nh hoÃ n tiá»n tá»± Ä‘á»™ng sang phÃ­a PayOS
        //        try
        //        {
        //            // Sinh mÃ£ Ä‘Æ¡n kiá»ƒu long phá»¥c vá»¥ PayOS
        //            long orderCodeLong = long.Parse(order.OrderCode);

        //            // Khá»Ÿi táº¡o Object cáº¥u hÃ¬nh lá»‡nh hoÃ n tiá»n theo SDK PayOS má»›i nháº¥t
        //            var refundRequest = new RefundRequest(
        //                amount: (int)order.FinalAmount, // Sá»‘ tiá»n cáº§n tráº£ láº¡i cho khÃ¡ch
        //                description: $"Hoan tien don #{order.OrderCode.Substring(0, 5)} do chu quay tu choi"
        //            );

        //            // Tiáº¿n hÃ nh gá»i lá»‡nh lÃªn mÃ¢y cá»§a PayOS
        //            RefundResult refundResult = await _payOSClient.(orderCodeLong, refundRequest);

        //            if (refundResult.Status == "REJECTED") // PhÃ­a ngÃ¢n hÃ ng/PayOS xá»­ lÃ½ xong ngay láº­p tá»©c
        //            {
        //                // BÆ°á»›c C: HoÃ n tiá»n thÃ nh cÃ´ng má»¹ mÃ£n -> Äá»•i sang tráº¡ng thÃ¡i ÄÃ£ hoÃ n tiá»n
        //                order.PayStatus = PayOrderStatus.Refunded; // Thuá»™c tÃ­nh Refunded = 3 cá»§a báº¡n

        //                if (successPayment != null)
        //                {
        //                    successPayment.Status = PaymentStatus.Refunded; // LÆ°u váº¿t báº£ng lá»‹ch sá»­ giao dá»‹ch
        //                    successPayment.UpdatedAt = DateTime.UtcNow;
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            // Náº¿u cÃ³ lá»—i máº¡ng hoáº·c lá»—i tá»« phÃ­a PayOS, Ä‘Æ¡n hÃ ng váº«n treo á»Ÿ dáº¡ng "RefundPending" Ä‘á»ƒ Admin vÃ o xá»­ lÃ½ tay sau
        //            await _orderRepo.UpdateOrderAsync(order);
        //            await _orderRepo.SaveChangesAsync();
        //            throw new Exception($"Chá»§ quáº§y tá»« chá»‘i Ä‘Æ¡n thÃ nh cÃ´ng, nhÆ°ng lá»‡nh hoÃ n tiá»n tá»± Ä‘á»™ng PayOS gáº·p sá»± cá»‘: {ex.Message}");
        //        }
        //    }

        //    // 4. LÆ°u toÃ n bá»™ thay Ä‘á»•i cáº­p nháº­t tráº¡ng thÃ¡i vÃ o Database
        //    await _orderRepo.UpdateOrderAsync(order);
        //    await _orderRepo.SaveChangesAsync();

        //    // 5. REAL-TIME (Sá»¬ Dá»¤NG KÃ‰ KHUNG Cá»¦A CHá»¦ NHÃ“M):
        //    // Báº¯n thÃ´ng bÃ¡o ngÆ°á»£c láº¡i cho mÃ¡y cá»§a KHÃCH HÃ€NG (order.CustomerId) Ä‘á»ƒ thÃ´ng bÃ¡o tin buá»“n Ä‘Æ¡n bá»‹ há»§y
        //    var notificationPayload = new NotificationListItemResponse
        //    {
        //        Id = Guid.NewGuid(),
        //        BoothId = order.BoothOwnerId,
        //        Type = "ORDER_REJECTED", // MÃ£ riÃªng Ä‘á»ƒ FE xá»­ lÃ½ Ä‘á»•i mÃ u Ä‘á»
        //        Title = "ÄÆ¡n hÃ ng bá»‹ tá»« chá»‘i",
        //        Content = order.PayStatus == PayOrderStatus.Refunded
        //            ? $"Ráº¥t tiáº¿c, quáº§y Ä‘Ã£ tá»« chá»‘i Ä‘Æ¡n #{order.OrderCode} cá»§a báº¡n do: {dto.Reason}. Tiá»n Ä‘Ã£ Ä‘Æ°á»£c hoÃ n láº¡i vÃ­ cá»§a báº¡n!"
        //            : $"Ráº¥t tiáº¿c, quáº§y Ä‘Ã£ tá»« chá»‘i Ä‘Æ¡n #{order.OrderCode} cá»§a báº¡n do: {dto.Reason}.",
        //        IsRead = false,
        //        ReferenceType = "Order",
        //        ReferenceId = order.Id,
        //        CreatedAt = DateTime.UtcNow
        //    };

        //    // Báº¯n Ä‘Ã­ch danh Ä‘áº¿n mÃ¡y cá»§a Customer thÃ´ng qua Realtime Publisher cÃ³ sáºµn cá»§a nhÃ³m
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
                return ApiResponse<bool>.Failure("Báº¡n khÃ´ng cÃ³ quyá»n chá»‰nh sá»­a Ä‘Æ¡n hÃ ng cá»§a quáº§y khÃ¡c!");
            }

            if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Completed)
            {
                return ApiResponse<bool>.Failure($"Đơn hàng đã đóng (Trạng thái hiện tại: {order.Status}). Không thể chỉnh sửa thêm.");
            }

            //Xử lý dựa trên loại thanh toán: Nếu là tiền mặt thì khi quầy bấm "Hoàn thành" thì tự động cập nhật Payment sang Paid, nếu là PayOS thì phải chờ Webhook từ PayOS về mới được phép hoàn thành
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
                return ApiResponse<bool>.Failure("Không tìm thấy thông tin thanh toán của đơn hàng!");
            }

            if (dto.NewStatus == OrderStatus.Preparing)
            {
                if (payment.Type == PaymentType.PayOS)
                {
                    // Nếu khách trả thiếu -> Chủ quán bấm nút này đồng nghĩa với việc CHẤP NHẬN BÙ TIỀN THIẾU
                    if (payment.Status == PaymentStatus.Underpaid)
                        return ApiResponse<bool>.Failure("Underpaid PayOS orders require manual refund and cannot be accepted.");
                    // Nếu khách chưa thanh toán đồng nào -> CHẶN TUYỆT ĐỐI không cho làm món
                    else if (payment.Status != PaymentStatus.Paid)
                    {
                        return ApiResponse<bool>.Failure("Khách đặt online chưa thanh toán thành công. Không thể duyệt làm món!");
                    }
                }
            }

            if (dto.NewStatus == OrderStatus.Completed)
            {
                if (payment.Type == PaymentType.Cash)
                {
                    // Tiền mặt: Khách ăn xong trả tiền -> Thu tiền thành công
                    payment.Status = PaymentStatus.Paid;
                    payment.PaidAt = DateTime.UtcNow;
                    payment.UpdatedAt = DateTime.UtcNow;
                }
                else if (payment.Type == PaymentType.PayOS)
                {
                    // PayOS: Nếu đến bước này mà trạng thái tài chính vẫn chưa thành Paid (lọt lưới logic) -> CHẶN LẠI
                    if (payment.Status != PaymentStatus.Paid)
                    {
                        return ApiResponse<bool>.Failure("Đơn hàng online chưa hoàn tất dòng tiền thành công. Không thể hoàn thành!");
                    }
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

            if (order.CustomerId != walkInId)
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

                    // Báº¯n Ä‘Ã­ch danh vÃ o Group SignalR cá»§a khÃ¡ch hÃ ng (TÃªn group chÃ­nh lÃ  CustomerId)
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

        //Khách chủ động hủy đơn hàng trước khi quầy nhận đơn (Chỉ áp dụng cho khách đặt qua App, không áp dụng cho khách vãng lai)
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
            if (order == null) return ApiResponse<bool>.Failure("ÄÆ¡n hÃ ng khÃ´ng tá»“n táº¡i", data: false);

            // Chá»‰ cho phÃ©p há»§y khi Ä‘Æ¡n Ä‘ang á»Ÿ tráº¡ng thÃ¡i chá» thanh toÃ¡n (Pending)

            if (order.Status != OrderStatus.Placed)
            {
                await _orderRepo.RollbackTransactionAsync();
                return ApiResponse<bool>.Failure($"ÄÆ¡n hÃ ng khÃ´ng thá»ƒ há»§y á»Ÿ tráº¡ng thÃ¡i {order.Status}", data: false);
            }

            var payment = order.Payments
                               .OrderByDescending(p => p.CreatedAt)
                               .FirstOrDefault(); // lấy cái đầu tiên

            if (payment == null)
            {
                await _orderRepo.RollbackTransactionAsync();
                return ApiResponse<bool>.Failure("Không tìm thấy bản ghi thanh toán Pending để hủy đơn", data: false);
            }

            if (payment.Type == PaymentType.PayOS)
            {
                try
                {
                    // Chủ động gọi PayOS đóng link thanh toán, chặn không cho quét QR nữa
                    await _payos.CancelPaymentLinkAsync(order.OrderCode);
                }
                catch (Exception ex)
                {
                    // Ghi log lỗi nhưng KHÔNG chặn tiến trình cập nhật Database nội bộ
                    _logger.LogWarning(ex, $"Không thể đóng link thanh toán trên PayOS cho đơn #{orderCode}. Có thể link đã hết hạn hoặc không tồn tại.");
                }
            }

            try
            {
                // Cập nhật Database
                payment.Status = PaymentStatus.Cancelled;
                payment.UpdatedAt = DateTime.UtcNow;

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
                _logger.LogError(ex, $"Lỗi xảy ra khi cập nhật DB hủy đơn hàng #{orderCode}");
                return ApiResponse<bool>.Failure($"Lỗi hệ thống khi cập nhật trạng thái hủy đơn. Lỗi: {ex.Message}", data: false);
            }
        }

        //Chủ quán hủy đơn hàng (Chỉ áp dụng cho quầy, không áp dụng cho khách đặt qua App)
        //Có 2 trường hợp :
        //1) Nếu khách trả tiền mặt thì quầy hủy là xong,
        //2) Nếu khách trả online thì quầy hủy phải chạy luồng hoàn tiền sang PayOS
#if false // Replaced by the recoverable implementation below.
        public async Task<ApiResponse<bool>> CancelOrderByBoothOwnerLegacyAsync(Guid boothOwnerId, long orderCode, RefundQRRequest request)
        {
            // 1. Kiểm tra request hợp lệ ngay từ đầu
            if (request == null) return ApiResponse<bool>.Failure("Dữ liệu yêu cầu không hợp lệ.", data: false);

            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order is not null && order.BoothOwnerId != boothOwnerId)
                throw AppException.Forbidden("You cannot cancel an order owned by another booth.", "ORDER_ACCESS_DENIED");
            if (order == null) return ApiResponse<bool>.Failure("Đơn hàng không tồn tại", data: false);

            // Chủ quán KHÔNG được hủy đơn đã hoàn thành hoặc đã hủy
            if (order.Status == OrderStatus.Completed || order.Status == OrderStatus.Cancelled)
            {
                return ApiResponse<bool>.Failure("Đơn hàng đã hoàn tất hoặc đã được hủy trước đó.", data: false);
            }

            // Tìm bản ghi thanh toán thành công (nếu có)
            var paidPayment = order.Payments
                                   .OrderByDescending(p => p.CreatedAt)
                                   .FirstOrDefault(p => p.Status == PaymentStatus.Paid || p.Status == PaymentStatus.Underpaid);

            string notificationTitle;
            string notificationContent;
            var requiresExternalRefund = paidPayment is not null
                && paidPayment.Gateway != PaymentGateway.None
                && paidPayment.Amount > 0m;

            // LUỒNG 1: ĐƠN HÀNG ĐÃ THANH TOÁN ONLINE -> KHỞI TẠO HOÀN TIỀN
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
                    return ApiResponse<bool>.Failure("Đơn hàng đã thanh toán. Vui lòng cung cấp đầy đủ Số tài khoản và Mã ngân hàng để hoàn tiền.", data: false);
                }

                try
                {
                    long refundAmount = paidPayment.Status == PaymentStatus.Underpaid
                            ? (long)paidPayment.Amount //Số tiền thực tế khách đã trả (trường hợp thanh toán thiếu)
                            : (long)order.FinalAmount; //Số tiền cần hoàn lại cho khách (thanh toán đầy đủ)

                    var referenceId = $"refund_{order.OrderCode}_{DateTime.UtcNow.Ticks}"; // Thêm Ticks để tránh trùng ID khi gọi lại nếu lỗi
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

                    // Gọi lệnh Payout sang PayOS
                    var payoutResult = await _payOutClient.Payouts.Batch.CreateAsync(payoutRequest);
                    _logger.LogInformation($"Yêu cầu Payout hoàn tiền đã được gửi lên PayOS cho đơn #{order.OrderCode}. Payout ID: {payoutResult.Id}");

                    //if (payoutResult != null && (payoutResult. == "COMPLETED" || payoutResult.Status == "SUCCESS"))
                    //{
                    //    // Tiền đã sang ngay lập tức -> Chuyển thẳng sang Refunded!
                    //    paidPayment.Status = PaymentStatus.Refunded;
                    //}
                    //else
                    //{
                    //    // Trường hợp lệnh đã ghi nhận nhưng bên Ngân hàng đang giữ lại xử lý
                    //    paidPayment.Status = PaymentStatus.RefundProcessing;
                    //}

                    paidPayment.Status = PaymentStatus.RefundProcessing;

                    // CHÚ Ý: Lúc này tiền chưa về tài khoản khách ngay, trạng thái đúng phải là RefundProcessing
                    //paidPayment.Status = PaymentStatus.RefundProcessing;
                    paidPayment.RefundReason = request.RefundReason;
                    paidPayment.UpdatedAt = DateTime.UtcNow;

                    // Đơn hàng vật lý thì có thể chuyển sang Cancelled ngay lập tức để nhà bếp giải phóng đơn
                    order.Status = OrderStatus.Cancelled;
                    //order.Note = $"Chủ quán hủy đơn. Lý do: {request.RefundReason}. Đang chờ PayOS xử lý hoàn tiền.";
                    order.UpdatedAt = DateTime.UtcNow;

                    _orderRepo.Update(order);
                    await _orderRepo.SaveChangesAsync();

                    notificationTitle = "Đơn hàng đã bị hủy & Đang hoàn tiền";
                    notificationContent = $"Đơn hàng #{order.OrderCode} đã bị hủy. Lệnh hoàn tiền {refundAmount:N0}đ đang được xử lý qua PayOS. Lý do: {request.RefundReason}";

                    // Gửi thông báo cho khách hàng
                    //var notificationPayload = new NotificationListItemResponse
                    //{
                    //    Id = Guid.NewGuid(),
                    //    BoothId = order.BoothOwnerId,
                    //    Type = "ORDER_CANCELLED",
                    //    Title = "Đơn hàng đã bị hủy & Đang hoàn tiền",
                    //    Content = $"Đơn hàng #{order.OrderCode} đã bị hủy. Lệnh hoàn tiền {order.FinalAmount:N0}đ đang được xử lý. Lý do: {request.RefundReason}",
                    //    IsRead = false,
                    //    ReferenceType = "Order",
                    //    ReferenceId = order.Id,
                    //    CreatedAt = DateTime.UtcNow
                    //};
                    //await _notificationPublisher.PublishAsync(order.CustomerId, notificationPayload, unreadCount: 1);

                    //return ApiResponse<bool>.SuccessResponse(true, "Chủ quán hủy đơn thành công. Hệ thống đang tiến hành hoàn tiền qua PayOS.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi khi gọi API hoàn tiền PayOS cho đơn #{order.OrderCode}");
                    _logger.LogError($"Data (nếu có): {ex.Data}");
                    _logger.LogError($"Full Exception details: {ex}");
                    return ApiResponse<bool>.Failure($"Gọi lệnh hoàn tiền sang PayOS thất bại. Vui lòng kiểm tra lại số tài khoản khách hoặc số dư ví PayOS. Lỗi: {ex.Message}", data: false);
                }
            }
            else
            {
                // LUỒNG 2: ĐƠN HÀNG CHƯA THANH TOÁN (CASH HOẶC PAYOS CHƯA QUÉT MÃ)
                // Cập nhật tất cả các bản ghi thanh toán chưa thành công thành Cancelled
                var unPaidPayments = order.Payments.Where(p => p.Status == PaymentStatus.Pending).ToList();
                foreach (var p in unPaidPayments)
                {
                    p.Status = PaymentStatus.Cancelled;
                    p.RefundReason = request.RefundReason;
                    p.UpdatedAt = DateTime.UtcNow;
                }

                order.Status = OrderStatus.Cancelled;
                //order.Note = $"Chủ quán hủy đơn chưa thanh toán. Lý do: {request.RefundReason}";
                order.UpdatedAt = DateTime.UtcNow;

                PromotionUsageLifecycle.ReleaseReserved(
                    order.PromotionUsages,
                    order.UpdatedAt);

                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();

                notificationTitle = "Đơn hàng đã bị hủy";
                notificationContent = $"Đơn hàng #{order.OrderCode} đã bị hủy bởi chủ quán. Lý do: {request.RefundReason}";
            }

            // Gửi thông báo cho khách hàng
            await PublishPersistedNotificationAsync(
                order.CustomerId,
                requiresExternalRefund
                    ? NotificationType.RefundPending
                    : NotificationType.OrderCancelled,
                notificationTitle,
                notificationContent,
                order.Id);

            return ApiResponse<bool>.SuccessResponse(true, requiresExternalRefund
                ? "Chủ quán hủy đơn thành công. Hệ thống đang tiến hành hoàn tiền qua PayOS."
                : "Đơn hàng đã được hủy thành công.");
        }


        //Tình huống khách đặt món payos, trả tiền, nhưng mạng lỗi và webhook không về kịp, khách bấm nút "Tôi đã thanh toán" trên FE để xác nhận, thì gọi API này để kiểm tra trạng thái thực tế từ PayOS
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

            // Nếu đơn đã xử lý thành công trước đó rồi thì thôi
            if (order.Status != OrderStatus.Placed &&  
                order.Status != OrderStatus.Cancelled) //order có status từ preparing, ready, completed thì coi như đã thanh toán thành công rồi
                return ApiResponse<bool>.SuccessResponse(true, "Đơn đã được thanh toán và đang xử lý.");

            try
            {
                // 1. CHỦ ĐỘNG GỌI SANG PAYOS ĐỂ KIỂM TRA (Không đợi Webhook)
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
                        //order.Status = OrderStatus.Underpaid;
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
        //                                                      string customerBankBin,  //Mã BIN ngân hàng (6 số đầu) của khách để PayOS đối chiếu, nếu có
        //                                                      string customerAccountNumber) //Số tài khoản ngân hàng của khách để PayOS đối chiếu, nếu có
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
                        ? "Có đơn hàng được giảm 100%"
                        : "Có đơn hàng tiền mặt mới",
                    $"Đơn #{order.OrderCode}, số tiền {order.FinalAmount:N0}đ.",
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
                    "Đơn hàng đã thanh toán",
                    $"Đơn #{order.OrderCode} đã thanh toán {order.FinalAmount:N0}đ.",
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

        public async Task<bool> HasOrderWithCodeAsync(long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            return order != null;
        }

    }
}
