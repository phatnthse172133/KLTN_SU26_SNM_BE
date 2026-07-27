using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.PayOS;
using ApplicationLayer.Services.Promotions;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly IPromotionValidationService _promotionValidation;
        private readonly IPayOSService _payos;
        private readonly IRealtimeNotificationPublisher _notificationPublisher;
        private readonly IFoodItemRepository _foodItemRepo;
        private readonly ILogger<OrderService> _logger;
        private readonly IConfiguration _config;
        private readonly IPayOSOrderCodeGenerator _orderCodeGenerator;
        private readonly IBoothRepository _boothRepo;

        public OrderService(
            IOrderRepository orderRepo,
            IPromotionRepository promotionRepo,
            IPromotionValidationService promotionValidation,
            IPayOSService payos,
            IRealtimeNotificationPublisher notificationPublisher,
            IFoodItemRepository foodItemRepo,
            ILogger<OrderService> logger,
            IConfiguration config,
            IPayOSOrderCodeGenerator orderCodeGenerator,
            IBoothRepository boothRepo)
        {
            _orderRepo = orderRepo;
            _promotionRepo = promotionRepo;
            _promotionValidation = promotionValidation;
            _payos = payos;
            _notificationPublisher = notificationPublisher;
            _foodItemRepo = foodItemRepo;
            _config = config;
            _logger = logger;
            _orderCodeGenerator = orderCodeGenerator;
            _boothRepo = boothRepo;
        }

        //Dành cho customer lẫn khách vang lai (Walk-in) đặt món, trả về link thanh toán nếu chọn online
        public async Task<ApiResponse<OrderResponseDto>> CreateOrderAsync(CreateOrderDto dto)
        {
            // 0. Guard: reject orders for booths in deleted markets
            var booth = await _boothRepo.GetByOwnerIdAsync(dto.BoothOwnerId);
            if (booth is null)
                return ApiResponse<OrderResponseDto>.Failure("Booth not found.", "BOOTH_NOT_FOUND");
            if (booth.Status != BoothStatus.Active)
                return ApiResponse<OrderResponseDto>.Failure("This booth is not currently active. Orders cannot be placed.", "BOOTH_NOT_ACTIVE");
            if (booth.NightMarket is null || booth.NightMarket.IsDeleted)
                return ApiResponse<OrderResponseDto>.Failure("This night market is no longer available. Orders cannot be placed.", "MARKET_UNAVAILABLE");

            // 1. Sinh mã đơn hàng dạng Số nguyên (Duy nhất) vì PayOS ép buộc mã đơn là kiểu long/int
            long uniqueOrderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.Order);

            // XỬ LÝ KHÁCH HÀNG VÃNG LAI: Nếu chủ quầy đặt hộ và không có CustomerId cụ thể
            Guid? finalCustomerId = dto.CustomerId;
            if (dto.IsCreatedByBooth && finalCustomerId == null)
            {
                // Đọc từ file appsettings.json ra, nếu file config lỗi thì dùng giá trị mặc định để backup
                var walkInIdString = _config["SystemSettings:WalkInCustomerId"]
                                     ?? "00000000-0000-0000-0000-000000000001";
                finalCustomerId = Guid.Parse(walkInIdString);
            }
            if (!finalCustomerId.HasValue)
                return ApiResponse<OrderResponseDto>.Failure(
                    "CustomerId is required for customer-created orders.",
                    "CUSTOMER_ID_REQUIRED");

            // 2. Khởi tạo đối tượng Order chính
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = finalCustomerId.Value,
                BoothOwnerId = dto.BoothOwnerId,
                OrderCode = uniqueOrderCode,
                Note = dto.Note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Status = OrderStatus.Placed
            };

            // 3. Duyệt danh sách món ăn + Topping để lưu chi tiết và tính tổng tiền thực tế
            decimal calculatedTotalAmount = 0;

            //REAL
            var foodIds = dto.Items.Select(i => i.FoodItemId).ToList();
            var foodItemsFromDb = await _foodItemRepo.GetAllFoodItemsByIdsAsync(foodIds);

            //TEST (tạm thời comment 2 dòng trên và 4 dòng dưới để test PayOS, tránh lỗi null ref khi chưa có món ăn thực tế trong DB)


            foreach (var itemDto in dto.Items)
            {
                var dbFoodItem = foodItemsFromDb.FirstOrDefault(f => f.Id == itemDto.FoodItemId);
                if (dbFoodItem == null) return ApiResponse<OrderResponseDto>.Failure("Món ăn không tồn tại hoặc đã bị xóa khỏi thực đơn!");

                decimal realUnitPrice = dbFoodItem.Price;
                decimal itemTotalPrice = realUnitPrice * itemDto.Quantity;
                //decimal realUnitPrice = itemDto.UnitPrice;
                //decimal itemTotalPrice = realUnitPrice * itemDto.Quantity;

                var orderDetail = new OrderDetail
                {
                    Id = Guid.NewGuid(),
                    FoodItemId = itemDto.FoodItemId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = realUnitPrice, // Snapshot giá món
                    TotalPrice = itemTotalPrice,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Xử lý đống Topping đi kèm của món ăn đó (Nếu có)
                //foreach (var toppingDto in itemDto.Toppings)
                //{
                //    var orderDetailTopping = new OrderDetailTopping
                //    {
                //        Id = Guid.NewGuid(),
                //        ToppingItemId = toppingDto.ToppingItemId,
                //        ToppingName = toppingDto.ToppingName,
                //        UnitPrice = toppingDto.UnitPrice, // Snapshot giá topping
                //    };

                //    orderDetail.OrderDetailToppings.Add(orderDetailTopping);

                //    // Cộng dồn tiền Topping vào tổng tiền của món (Nhân với số lượng món ăn đặt)
                //    itemTotalPrice += (toppingDto.UnitPrice * itemDto.Quantity);
                //}

                // Cập nhật lại chính xác TotalPrice của OrderDetail sau khi có topping
                //orderDetail.TotalPrice = itemTotalPrice;

                // Cộng vào tổng tiền lớn của cả Đơn hàng
                calculatedTotalAmount += itemTotalPrice;

                // Add vào Collection có sẵn trong Entity Order
                order.OrderDetails.Add(orderDetail);
            }

            // 4. Áp đặt số tiền cuối cùng cho Đơn hàng
            decimal discountAmount = 0;
            if (!string.IsNullOrWhiteSpace(dto.PromotionCode))
            {
                var promotion = await _promotionRepo.GetByCodeAsync(dto.BoothId, dto.PromotionCode);
                if (promotion is null)
                    return ApiResponse<OrderResponseDto>.Failure(
                        "The promotion code is invalid or unavailable.",
                        "PROMOTION_NOT_FOUND");

                try
                {
                    var validationItems = dto.Items.Select(itemDto =>
                    {
                        var food = foodItemsFromDb.First(f => f.Id == itemDto.FoodItemId);
                        return new CartItem
                        {
                            FoodItemId = itemDto.FoodItemId,
                            Quantity = itemDto.Quantity,
                            FoodItem = food
                        };
                    }).ToList();
                    var validationResult = await _promotionValidation.ValidateAsync(
                        finalCustomerId.Value,
                        promotion,
                        validationItems);
                    discountAmount = validationResult.DiscountAmount;
                    order.PromotionUsages.Add(new PromotionUsage
                    {
                        Id = Guid.NewGuid(),
                        PromotionId = promotion.Id,
                        OrderId = order.Id,
                        CustomerId = finalCustomerId.Value,
                        DiscountAmount = discountAmount,
                        Status = PromotionUsageStatus.Reserved,
                        AppliedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                catch (AppException ex)
                {
                    return ApiResponse<OrderResponseDto>.Failure(ex.Message, ex.ErrorCode);
                }
            }

            order.TotalAmount = calculatedTotalAmount;
            order.DiscountAmount = discountAmount;
            order.FinalAmount = calculatedTotalAmount - discountAmount;
            if (order.FinalAmount < 0) order.FinalAmount = 0; // Tránh tiền bị âm

            // 5. Khởi tạo bản ghi lịch sử giao dịch ở bảng Payment
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                BoothOwnerId = dto.BoothOwnerId,
                Amount = order.FinalAmount,
                Type = dto.PaymentMethod,
                Gateway = dto.PaymentMethod == PaymentType.PayOS
                    ? PaymentGateway.Payos
                    : PaymentGateway.BankTransfer,
                Status = PaymentStatus.Pending, 
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            order.Payments.Add(payment);

            // 6. RẼ NHÁNH LOGIC THANH TOÁN
            string? checkoutUrl = null;
            NotificationListItemResponse? newOrderNotification = null;

            if (dto.PaymentMethod == PaymentType.Cash)
            {
                if (!dto.IsCreatedByBooth)
                {
                    newOrderNotification = new NotificationListItemResponse
                    {
                        Id = Guid.NewGuid(),
                        BoothId = order.BoothOwnerId,
                        Type = "ORDER_NEW",
                        Title = "New cash order",
                        Content = $"Order #{order.OrderCode} was placed for {order.FinalAmount:N0} VND.",
                        IsRead = false,
                        ReferenceType = "Order",
                        ReferenceId = order.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                }
            }
            else if (dto.PaymentMethod == PaymentType.PayOS)
            {
                try
                {
                    var payosResp = await _payos.CreatePaymentLinkAsync(new PayOSPaymentRequest
                    {
                        OrderCode = uniqueOrderCode,
                        Amount = order.FinalAmount,
                        Description = $"Process {uniqueOrderCode}",
                    });

                    payment.CheckoutUrl = payosResp.CheckoutUrl;
                    payment.PaymentLinkId = payosResp.PaymentLinkId;
                    payment.PayOSOrderCode = uniqueOrderCode;
                    checkoutUrl = payosResp.CheckoutUrl;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create PayOS payment link for order creation");
                    return ApiResponse<OrderResponseDto>.Failure("Failed to create payment link. Please try again later.", "PAYMENT_LINK_FAILED");
                }
            }

            // 7. Lưu trọn gói Đơn hàng + Chi tiết đơn + Topping + Lịch sử Payment vào DB (Chỉ 1 lần Save duy nhất)
            await _orderRepo.AddAsync(order);
            await _orderRepo.SaveChangesAsync();
            if (newOrderNotification is not null)
            {
                try
                {
                    await _notificationPublisher.PublishAsync(
                        order.BoothOwnerId, newOrderNotification, unreadCount: 1);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Order {OrderCode} was saved, but its realtime notification could not be delivered.",
                        order.OrderCode);
                }
            }

            // 8. Trả kết quả về cho Controller
            return ApiResponse<OrderResponseDto>.SuccessResponse(
                new OrderResponseDto
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    Status = order.Status,
                    PaymentUrl = checkoutUrl
                },
                "Order created successfully."
            );
        }

        public async Task<ApiResponse<SupplementalPaymentResponseDto>> PayRemainingAmountAsync(Guid actorId, long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order == null)
                return ApiResponse<SupplementalPaymentResponseDto>.Failure("Order not found.", "ORDER_NOT_FOUND");

            if (order.CustomerId != actorId && order.BoothOwnerId != actorId)
                return ApiResponse<SupplementalPaymentResponseDto>.Failure("You do not have permission to perform this action on this order.", "ORDER_ACCESS_DENIED");

            if (order.Status != OrderStatus.Underpaid)
                return ApiResponse<SupplementalPaymentResponseDto>.Failure("Order is not in Underpaid status.", "ORDER_NOT_UNDERPAID");

            await _orderRepo.BeginTransactionAsync();
            long? createdPayOSOrderCode = null;
            try
            {
                await _orderRepo.AcquireSupplementalPaymentLockAsync(order.Id);

                // Reload order inside transaction to get consistent state
                order = await _orderRepo.GetOrderByCodeAsync(orderCode);
                if (order == null)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return ApiResponse<SupplementalPaymentResponseDto>.Failure("Order not found.", "ORDER_NOT_FOUND");
                }

                if (order.CustomerId != actorId && order.BoothOwnerId != actorId)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return ApiResponse<SupplementalPaymentResponseDto>.Failure(
                        "You do not have permission to perform this action on this order.",
                        "ORDER_ACCESS_DENIED");
                }

                if (order.Status != OrderStatus.Underpaid)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return ApiResponse<SupplementalPaymentResponseDto>.Failure(
                        "Order is not in Underpaid status.",
                        "ORDER_NOT_UNDERPAID");
                }

                var totalPaid = await _orderRepo.GetTotalPaidAmountAsync(orderCode);
                var remaining = order.FinalAmount - totalPaid;

                if (remaining <= 0)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return ApiResponse<SupplementalPaymentResponseDto>.Failure("Order is already fully paid.", "ORDER_ALREADY_FULLY_PAID");
                }

                // Check for existing pending PayOS payment — return its link if still valid
                var existingPending = await _orderRepo.GetPendingPayOSPaymentByOrderIdAsync(order.Id);
                if (existingPending != null && !string.IsNullOrEmpty(existingPending.CheckoutUrl))
                {
                    await _orderRepo.RollbackTransactionAsync();
                    return ApiResponse<SupplementalPaymentResponseDto>.SuccessResponse(
                        new SupplementalPaymentResponseDto
                        {
                            OrderId = order.Id,
                            OrderCode = order.OrderCode,
                            RemainingAmount = existingPending.Amount,
                            TotalPaid = totalPaid,
                            FinalAmount = order.FinalAmount,
                            PaymentUrl = existingPending.CheckoutUrl,
                            PayOSOrderCode = existingPending.PayOSOrderCode ?? 0
                        },
                        "A pending supplemental payment link already exists for this order."
                    );
                }

                var supplementalOrderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.Order);

                PayOSPaymentResponse payosResp;
                try
                {
                    payosResp = await _payos.CreatePaymentLinkAsync(new PayOSPaymentRequest
                    {
                        OrderCode = supplementalOrderCode,
                        Amount = remaining,
                        Description = $"Supplement {orderCode}",
                    });
                    createdPayOSOrderCode = supplementalOrderCode;
                }
                catch (Exception ex)
                {
                    await _orderRepo.RollbackTransactionAsync();
                    _logger.LogError(ex, "Failed to create supplemental payment link for order {OrderCode}", orderCode);
                    return ApiResponse<SupplementalPaymentResponseDto>.Failure(
                        "Failed to create supplemental payment link. Please try again later.",
                        "SUPPLEMENTAL_PAYMENT_LINK_FAILED");
                }

                var now = DateTime.UtcNow;
                var payment = existingPending ?? new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    BoothOwnerId = order.BoothOwnerId,
                    CreatedAt = now
                };
                payment.Amount = remaining;
                payment.Type = PaymentType.PayOS;
                payment.Gateway = PaymentGateway.Payos;
                payment.Status = PaymentStatus.Pending;
                payment.CheckoutUrl = payosResp.CheckoutUrl;
                payment.PaymentLinkId = payosResp.PaymentLinkId;
                payment.PayOSOrderCode = supplementalOrderCode;
                payment.UpdatedAt = now;

                if (existingPending is null)
                    await _orderRepo.AddPaymentAsync(payment);
                await _orderRepo.SaveChangesAsync();
                await _orderRepo.CommitTransactionAsync();
                createdPayOSOrderCode = null;

                return ApiResponse<SupplementalPaymentResponseDto>.SuccessResponse(
                    new SupplementalPaymentResponseDto
                    {
                        OrderId = order.Id,
                        OrderCode = order.OrderCode,
                        RemainingAmount = remaining,
                        TotalPaid = totalPaid,
                        FinalAmount = order.FinalAmount,
                        PaymentUrl = payosResp.CheckoutUrl,
                        PayOSOrderCode = supplementalOrderCode
                    },
                    "Supplemental payment link created successfully."
                );
            }
            catch (Exception ex)
            {
                await _orderRepo.RollbackTransactionAsync();
                if (createdPayOSOrderCode.HasValue)
                {
                    try
                    {
                        await _payos.CancelPaymentLinkAsync(createdPayOSOrderCode.Value);
                    }
                    catch (Exception cancelException)
                    {
                        _logger.LogWarning(
                            cancelException,
                            "Failed to cancel orphan supplemental PayOS link {PayOSOrderCode}",
                            createdPayOSOrderCode.Value);
                    }
                }
                _logger.LogError(ex, "Error creating supplemental payment for order {OrderCode}", orderCode);
                return ApiResponse<SupplementalPaymentResponseDto>.Failure(
                    "An error occurred while processing the supplemental payment.",
                    "SUPPLEMENTAL_PAYMENT_LINK_FAILED");
            }
        }

        public async Task<WebhookDispatchResult> ProcessPaymentWebhookAsync(PayOSWebhookData verifiedData)
        {
            if (verifiedData == null)
            {
                _logger.LogWarning("Webhook received null verified data.");
                return WebhookDispatchResult.InvalidSignature;
            }

            try
            {
                if (!verifiedData.IsSuccessful)
                {
                    _logger.LogInformation("Webhook received non-success code: {Code} for order {OrderCode}. Skipping order update.", verifiedData.Code, verifiedData.OrderCode);
                    return WebhookDispatchResult.NotSuccessful;
                }

                // Try to find a Payment by PayOSOrderCode first (supplemental payment flow)
                var paymentByCode = await _orderRepo.GetPaymentByPayOSOrderCodeAsync(verifiedData.OrderCode);
                Order? order;

                if (paymentByCode != null && paymentByCode.Order != null)
                {
                    order = paymentByCode.Order;
                    _logger.LogInformation("Webhook matched Payment {PaymentId} by PayOSOrderCode {OrderCode} for Order #{RealOrderCode}.",
                        paymentByCode.Id, verifiedData.OrderCode, order.OrderCode);
                }
                else
                {
                    order = await _orderRepo.GetOrderByCodeAsync(verifiedData.OrderCode);
                }

                if (order == null)
                {
                    _logger.LogWarning("No order or payment found matching OrderCode: {OrderCode} from Webhook.", verifiedData.OrderCode);
                    return WebhookDispatchResult.NotFound;
                }

                if (order.Status != OrderStatus.Placed && order.Status != OrderStatus.Underpaid)
                {
                    _logger.LogInformation("Order #{OrderCode} already processed (current status: {Status}). Skipping duplicate.", order.OrderCode, order.Status);
                    return WebhookDispatchResult.AlreadyProcessed;
                }

                var now = DateTime.UtcNow;

                // Underpaid flow: first webhook with insufficient amount, or supplemental payment for an already-Underpaid order
                if (order.Status == OrderStatus.Underpaid)
                {
                    // Supplemental payment for an already-underpaid order
                    await _orderRepo.BeginTransactionAsync();
                    try
                    {
                        // Mark the pending payment as paid with the received amount
                        var paymentRows = await _orderRepo.UpdatePaymentToPaidWithAmountAsync(
                            order.OrderCode, verifiedData.Amount, verifiedData.PaymentLinkId!, verifiedData.Reference, now, now);
                        if (paymentRows == 0)
                        {
                            await _orderRepo.RollbackTransactionAsync();
                            _logger.LogInformation("Order #{OrderCode} supplemental webhook: no pending Payment record found. Skipping.", order.OrderCode);
                            return WebhookDispatchResult.AlreadyProcessed;
                        }

                        // Check if total paid amount now covers the order
                        var totalPaid = await _orderRepo.GetTotalPaidAmountAsync(order.OrderCode);

                        if (totalPaid >= order.FinalAmount)
                        {
                            // Fully paid — transition to Preparing
                            var orderRows = await _orderRepo.UpdateOrderFromUnderpaidToPreparingAsync(order.OrderCode, now);
                            if (orderRows == 0)
                            {
                                await _orderRepo.RollbackTransactionAsync();
                                _logger.LogInformation("Order #{OrderCode} no longer Underpaid (concurrent update). Skipping.", order.OrderCode);
                                return WebhookDispatchResult.AlreadyProcessed;
                            }
                        }

                        await _orderRepo.CommitTransactionAsync();
                    }
                    catch
                    {
                        await _orderRepo.RollbackTransactionAsync();
                        throw;
                    }

                    // Send notification
                    var totalPaidAfter = await _orderRepo.GetTotalPaidAmountAsync(order.OrderCode);
                    var notificationType = totalPaidAfter >= order.FinalAmount ? "ORDER_PAID" : "ORDER_UNDERPAID";
                    var notificationTitle = totalPaidAfter >= order.FinalAmount ? "Order paid (supplemental)" : "Order still underpaid";
                    var notificationContent = totalPaidAfter >= order.FinalAmount
                        ? $"Order #{order.OrderCode} has been fully paid via supplemental payment. Total received: {totalPaidAfter:N0}, Required: {order.FinalAmount:N0}"
                        : $"Order #{order.OrderCode} received supplemental payment of {verifiedData.Amount:N0}. Total received so far: {totalPaidAfter:N0}, Required: {order.FinalAmount:N0}";

                    var supplementalNotification = new NotificationListItemResponse
                    {
                        Id = Guid.NewGuid(),
                        BoothId = order.BoothOwnerId,
                        Type = notificationType,
                        Title = notificationTitle,
                        Content = notificationContent,
                        IsRead = false,
                        ReferenceType = "Order",
                        ReferenceId = order.Id,
                        CreatedAt = now
                    };

                    try
                    {
                        await _notificationPublisher.PublishAsync(order.BoothOwnerId, supplementalNotification, unreadCount: 1);
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogError(notifEx, "Order #{OrderCode} supplemental notification failed after commit. Order status already persisted.", order.OrderCode);
                    }
                    return WebhookDispatchResult.OrderHandled;
                }

                // order.Status == Placed — first webhook
                // Underpaid: amount received is less than order total
                if (verifiedData.Amount < order.FinalAmount)
                {
                    await _orderRepo.BeginTransactionAsync();
                    try
                    {
                        var rowsAffected = await _orderRepo.UpdateOrderToUnderpaidAsync(order.OrderCode, now);
                        if (rowsAffected == 0)
                        {
                            await _orderRepo.RollbackTransactionAsync();
                            _logger.LogInformation("Order #{OrderCode} already processed by concurrent webhook. Skipping.", order.OrderCode);
                            return WebhookDispatchResult.AlreadyProcessed;
                        }

                        // Mark payment as paid with the partial amount received
                        var paymentRows = await _orderRepo.UpdatePaymentToPaidWithAmountAsync(
                            order.OrderCode, verifiedData.Amount, verifiedData.PaymentLinkId!, verifiedData.Reference, now, now);
                        if (paymentRows == 0)
                        {
                            await _orderRepo.RollbackTransactionAsync();
                            _logger.LogError("Order #{OrderCode} webhook: no pending Payment record found after marking order as Underpaid. Rolling back.", order.OrderCode);
                            return WebhookDispatchResult.NotFound;
                        }

                        await _orderRepo.CommitTransactionAsync();
                    }
                    catch
                    {
                        await _orderRepo.RollbackTransactionAsync();
                        throw;
                    }

                    var underpaidNotification = new NotificationListItemResponse
                    {
                        Id = Guid.NewGuid(),
                        BoothId = order.BoothOwnerId,
                        Type = "ORDER_UNDERPAID",
                        Title = "Order underpaid",
                        Content = $"Order {order.OrderCode} received insufficient payment. Required: {order.FinalAmount:N0}, Received: {verifiedData.Amount:N0}",
                        IsRead = false,
                        ReferenceType = "Order",
                        ReferenceId = order.Id,
                        CreatedAt = now
                    };

                    try
                    {
                        await _notificationPublisher.PublishAsync(order.BoothOwnerId, underpaidNotification, unreadCount: 1);
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogError(notifEx, "Order #{OrderCode} underpaid notification failed after commit. Order status already persisted.", order.OrderCode);
                    }
                    return WebhookDispatchResult.OrderHandled;
                }

                // Exact or overpaid: proceed with order activation
                if (verifiedData.Amount > order.FinalAmount)
                {
                    _logger.LogWarning("Order #{OrderCode} overpaid. Required: {Required}, Received: {Received}. Proceeding with order.", order.OrderCode, order.FinalAmount, verifiedData.Amount);
                }

                await _orderRepo.BeginTransactionAsync();
                try
                {
                    // Atomic conditional update: only transitions Placed → Preparing
                    var orderRows = await _orderRepo.UpdateOrderStatusIfPlacedAsync(order.OrderCode, OrderStatus.Preparing, now);
                    if (orderRows == 0)
                    {
                        await _orderRepo.RollbackTransactionAsync();
                        _logger.LogInformation("Order #{OrderCode} already processed by concurrent webhook. Skipping.", order.OrderCode);
                        return WebhookDispatchResult.AlreadyProcessed;
                    }

                    // Update payment record within the same transaction
                    var paymentRows = await _orderRepo.UpdatePaymentToPaidAsync(order.OrderCode, verifiedData.PaymentLinkId, verifiedData.Reference, now, now);
                    if (paymentRows == 0)
                    {
                        await _orderRepo.RollbackTransactionAsync();
                        _logger.LogError("Order #{OrderCode} webhook: no pending Payment record found. Rolling back order status update to keep Order and Payment consistent.", order.OrderCode);
                        return WebhookDispatchResult.NotFound;
                    }

                    await _orderRepo.CommitTransactionAsync();
                }
                catch
                {
                    await _orderRepo.RollbackTransactionAsync();
                    throw;
                }

                // Publish notification only after commit
                var paidNotification = new NotificationListItemResponse
                {
                    Id = Guid.NewGuid(),
                    BoothId = order.BoothOwnerId,
                    Type = "ORDER_PAID",
                    Title = "Order paid",
                    Content = $"Order #{order.OrderCode} has been paid successfully via PayOS. Amount: {order.FinalAmount:N0}",
                    IsRead = false,
                    ReferenceType = "Order",
                    ReferenceId = order.Id,
                    CreatedAt = now
                };

                try
                {
                    await _notificationPublisher.PublishAsync(order.BoothOwnerId, paidNotification, unreadCount: 1);
                }
                catch (Exception notifEx)
                {
                    _logger.LogError(notifEx, "Order #{OrderCode} paid notification failed after commit. Order status already persisted.", verifiedData.OrderCode);
                }
                return WebhookDispatchResult.OrderHandled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook for order {OrderCode}", verifiedData.OrderCode);
                throw;
            }
        }

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
            // 1. Lấy đơn hàng lên kèm thông tin giao dịch để kiểm tra dòng tiền
            var order = await _orderRepo.GetOrderByCodeAsync(dto.OrderCode);
            if (order == null) return ApiResponse<bool>.Failure("Không tìm thấy đơn hàng!");

            // 2. BẢO MẬT: Kiểm tra xem đơn này có thuộc về quầy của ông này không
            if (order.BoothOwnerId != boothOwnerId)
            {
                return ApiResponse<bool>.Failure("Bạn không có quyền chỉnh sửa đơn hàng của quầy khác!");
            }

            // 3. Chống gian lận tiền bạc
            if (dto.NewStatus == OrderStatus.Completed)
            {
                // Kiểm tra xem đơn này có bản ghi thanh toán thành công nào chưa
                bool isPaid = order.Payments.Any(p => p.Status == PaymentStatus.Paid);
                if (!isPaid)
                {
                    return ApiResponse<bool>.Failure("Không thể hoàn thành đơn hàng chưa được thanh toán thành công!");
                }
            }

            // 4. Cập nhật trạng thái
            order.Status = dto.NewStatus;
            order.UpdatedAt = DateTime.UtcNow;

            _orderRepo.Update(order);
            await _orderRepo.SaveChangesAsync();

            // 5. XỬ LÝ REALTIME "TING TING" QUA SIGNALR
            // Chỉ bắn tin cho khách hàng nếu đây là khách đặt qua App (có CustomerId cụ thể)
            // Nếu là ID khách vãng lai (toàn số 0) thì bỏ qua không cần bắn
            var walkInId = Guid.Parse(_config["SystemSettings:WalkInCustomerId"] ?? "00000000-0000-0000-0000-000000000001");

            if (order.CustomerId != Guid.Empty && order.CustomerId != walkInId)
            {
                string title = "";
                string content = "";

                // Tùy biến nội dung tin nhắn dựa theo từng trạng thái món ăn
                switch (order.Status)
                {
                    case OrderStatus.Preparing:
                        title = "Đơn hàng đang được chế biến!";
                        content = $"Quầy đã tiếp nhận và đang làm món cho đơn # {order.OrderCode} của bạn.";
                        break;
                    case OrderStatus.ReadyForPickup:
                        title = "Món ăn đã sẵn sàng! 🥳";
                        content = $"Đơn hàng # {order.OrderCode} đã làm xong. Bạn hãy đến quầy để nhận món nhé!";
                        break;
                    case OrderStatus.Completed:
                        title = "Cảm ơn bạn đã mua hàng! ❤️";
                        content = $"Đơn hàng # {order.OrderCode} đã được giao thành công. Chúc bạn ngon miệng!";
                        break;
                }

                if (!string.IsNullOrEmpty(title))
                {
                    var customerNotification = new NotificationListItemResponse
                    {
                        Id = Guid.NewGuid(),
                        BoothId = order.BoothOwnerId,
                        Type = $"ORDER_{order.Status.ToString().ToUpper()}", // ORDER_PREPARING, ORDER_READY, ORDER_COMPLETED
                        Title = title,
                        Content = content,
                        IsRead = false,
                        ReferenceType = "Order",
                        ReferenceId = order.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Bắn đích danh vào Group SignalR của khách hàng (Tên group chính là CustomerId)
                    await _notificationPublisher.PublishAsync(order.CustomerId, customerNotification, unreadCount: 1);
                }
            }

            return ApiResponse<bool>.SuccessResponse(true, "Cập nhật trạng thái đơn hàng thành công!");
        }

        //Khách chủ động hủy đơn hàng trước khi quầy nhận đơn (Chỉ áp dụng cho khách đặt qua App, không áp dụng cho khách vãng lai)
        public async Task<ApiResponse<bool>> CancelOrder(long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order == null) return ApiResponse<bool>.Failure("Đơn hàng không tồn tại", data: false);

            // Chỉ cho phép hủy khi đơn đang ở trạng thái chờ thanh toán (Pending)
            
            if (order.Status != OrderStatus.Placed)
            {
                return ApiResponse<bool>.Failure($"Đơn hàng không thể hủy ở trạng thái {order.Status}", data: false);
            }

            var payment = order.Payments
                               .OrderByDescending(p => p.CreatedAt)
                               .FirstOrDefault(p => p.Status == PaymentStatus.Pending); // Vừa lọc Pending vừa lấy cái đầu tiên

            try
            {
                // 1. GỌI SANG PAYOS ĐỂ HỦY LINK THANH TOÁN (Chặn không cho quét QR nữa)
                // Hàm này bắt buộc truyền OrderCode (kiểu long/int) và lý do hủy tùy ý
                if (payment != null)
                {
                    await _payos.CancelPaymentLinkAsync(order.OrderCode);
                    
                    // 2. Cập nhật Database
                    payment.Status = PaymentStatus.Cancelled;
                    payment.UpdatedAt = DateTime.UtcNow;
                }

                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();

                return ApiResponse<bool>.SuccessResponse(true, "Hủy đơn hàng thành công");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi xảy ra khi hủy đơn hàng #{orderCode}");
                return ApiResponse<bool>.Failure("Lỗi khi đồng bộ hủy đơn với PayOS. Vui lòng thử lại sau.", "CANCEL_ORDER_PAYOS_FAILED", data: false);
            }
        }

        //Tình huống khách đặt món payos, trả tiền, nhưng mạng lỗi và webhook không về kịp, khách bấm nút "Tôi đã thanh toán" trên FE để xác nhận, thì gọi API này để kiểm tra trạng thái thực tế từ PayOS
        public async Task<ApiResponse<bool>> ActiveCheckPaymentStatus(long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order == null) return ApiResponse<bool>.Failure("Đơn hàng không tồn tại", data: false);

            // Nếu đơn đã xử lý thành công trước đó rồi thì thôi
            if (order.Status != OrderStatus.Placed && 
                order.Status != OrderStatus.Underpaid && 
                order.Status != OrderStatus.Cancelled) //order có status từ preparing, ready, completed thì coi như đã thanh toán thành công rồi
                return ApiResponse<bool>.SuccessResponse(true, "Đơn đã được thanh toán và đang xử lý.");

            try
            {
                // 1. Actively check PayOS payment status (don't wait for webhook)
                var paymentInfo = await _payos.GetPaymentStatusAsync(orderCode);
                if (paymentInfo == null)
                    return ApiResponse<bool>.Failure("Could not retrieve payment status from PayOS.", data: false);

                // 2. If PayOS reports payment received (PAID)
                if (paymentInfo.Status == "Paid")
                {
                    if (paymentInfo.AmountPaid < (long)Math.Round(order.FinalAmount))
                    {
                        order.Status = OrderStatus.Underpaid;
                        order.UpdatedAt = DateTime.UtcNow;
                        _orderRepo.Update(order);
                        await _orderRepo.SaveChangesAsync();

                        var warningPayload = new NotificationListItemResponse
                        {
                            Id = Guid.NewGuid(),
                            BoothId = order.BoothOwnerId,
                            Type = "ORDER_UNDERPAID",
                            Title = "Order underpaid!",
                            Content = $"Order {order.OrderCode} payment verification detected underpayment. Required: {order.FinalAmount:N0}d, Received: {paymentInfo.AmountPaid:N0}d",
                            IsRead = false,
                            ReferenceType = "Order",
                            ReferenceId = order.Id,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _notificationPublisher.PublishAsync(order.BoothOwnerId, warningPayload, unreadCount: 1);

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

                    _orderRepo.Update(order);
                    await _orderRepo.SaveChangesAsync();

                    // 4. Chuẩn bị nội dung thông báo SignalR
                    string notificationTitle = previousStatus == OrderStatus.Cancelled
                        ? "Đơn đã hủy được thanh toán trễ!"
                        : "Đơn hàng đã thanh toán!";

                    string notificationContent = previousStatus == OrderStatus.Cancelled
                        ? $"Đơn hàng #{order.OrderCode} (từng bị hủy do quá hạn) vừa được đối soát thanh toán thành công qua PayOS. Số tiền: {order.FinalAmount:N0}đ"
                        : $"Đơn hàng #{order.OrderCode} đã được thanh toán thành công qua PayOS. Số tiền: {order.FinalAmount:N0}đ";

                    // Bắn SignalR báo cho chủ quầy "Ting Ting"
                    var notificationPayload = new NotificationListItemResponse
                    {
                        Id = Guid.NewGuid(),
                        BoothId = order.BoothOwnerId,
                        Type = "ORDER_PAID",          // Type dành cho đơn đã thanh toán online thành công
                        Title = notificationTitle,
                        Content = notificationContent,
                        IsRead = false,
                        ReferenceType = "Order",      // Định danh kiểu tham chiếu
                        ReferenceId = order.Id,       // Id của đơn hàng để FE click vào là xem được luôn
                        CreatedAt = DateTime.UtcNow
                    };

                    await _notificationPublisher.PublishAsync(order.BoothOwnerId, notificationPayload, unreadCount: 1);

                    return ApiResponse<bool>.SuccessResponse(true, "Bạn đã thanh toán thành công! Chủ quán đang chuẩn bị đơn hàng.");
                }

                return ApiResponse<bool>.Failure("Thanh toán chưa được ghi nhận trên hệ thống PayOS", data: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi xảy ra khi đối soát đơn hàng #{orderCode}");
                return ApiResponse<bool>.Failure("Lỗi khi đối soát với PayOS. Vui lòng thử lại sau.", "PAYOS_RECONCILIATION_FAILED", data: false);
            }
        }

        //public async Task<ApiResponse<bool>> RefundOrderAsync(long orderCode, 
        //                                                      string reason, 
        //                                                      string customerBankBin,  //Mã BIN ngân hàng (6 số đầu) của khách để PayOS đối chiếu, nếu có
        //                                                      string customerAccountNumber) //Số tài khoản ngân hàng của khách để PayOS đối chiếu, nếu có
        //{
        //    // 1. Tìm đơn hàng kèm danh sách thanh toán
        //    var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
        //    if (order == null) return ApiResponse<bool>.Failure("Đơn hàng không tồn tại", data: false);

        //    // 2. Kiểm tra trạng thái đơn hàng: Chỉ cho hoàn tiền khi đơn đã thanh toán thành công và quầy chưa hoàn tất phục vụ món (Chưa giao hàng)
        //    if (order.Status != OrderStatus.Preparing &&
        //        order.Status != OrderStatus.ReadyForPickup &&
        //        order.Status != OrderStatus.Underpaid) // khách trả thiếu cũng cần hoàn
        //    {
        //        return ApiResponse<bool>.Failure($"Đơn hàng ở trạng thái '{order.Status}' không thỏa mãn điều kiện để hoàn tiền.", data: false);
        //    }

        //    // 3. Tìm bản ghi Payment đã thanh toán thành công (Paid) để hoàn lại
        //    var paidPayment = order.Payments
        //                           .OrderByDescending(p => p.CreatedAt)
        //                           .FirstOrDefault(p => p.Status == PaymentStatus.Paid);

        //    if (paidPayment == null)
        //    {
        //        return ApiResponse<bool>.Failure("Không tìm thấy giao dịch đã thanh toán thành công của đơn hàng này để thực hiện hoàn tiền.", data: false);
        //    }

        //    try
        //    {
        //        // 4. LOGIC XỬ LÝ HOÀN TIỀN

        //        var referenceId = $"refund_{order.OrderCode}";

        //        var payoutRequest = new PayoutBatchRequest
        //        {
        //            ReferenceId = referenceId,
        //            Category = new List<string> { "refund" }, // Đổi category thành refund cho đúng nghiệp vụ
        //            ValidateDestination = true,               // Yêu cầu PayOS check xem tài khoản đích có thật không
        //            Payouts = new List<PayoutBatchItem>
        //            {
        //                new PayoutBatchItem
        //                {
        //                    ReferenceId = $"{referenceId}_item",
        //                    Amount = (long)order.FinalAmount, // Số tiền hoàn bằng đúng số tiền đơn hàng đã trả
        //                    Description = $"Hoan tien don hang #{order.OrderCode}", // Viết không dấu để tránh lỗi font ngân hàng
        //                    ToBin = customerBankBin,           // Truyền mã BIN ngân hàng khách
        //                    ToAccountNumber = customerAccountNumber // Số tài khoản khách
        //                }
        //            }
        //        };

        //        // Gọi lệnh Payout thực sự sang PayOS
        //        var payoutResult = await _payOSClient.Payouts.Batch.CreateAsync(payoutRequest);
        //        _logger.LogInformation($"Yêu cầu Payout hoàn tiền thành công cho đơn #{order.OrderCode}. Payout ID: {payoutResult.Id}");

        //        // Cập nhật trạng thái bảng thanh toán con sang Refunded (Đã hoàn tiền)
        //        paidPayment.Status = PaymentStatus.Refunded;
        //        paidPayment.UpdatedAt = DateTime.UtcNow;

        //        // Cập nhật trạng thái đơn hàng cha sang Refunded
        //        order.Status = OrderStatus.Refunded;
        //        order.UpdatedAt = DateTime.UtcNow;

        //        _orderRepo.Update(order);
        //        await _orderRepo.SaveChangesAsync();

        //        // 5. Bắn thông báo SignalR xuống Client (Cả chủ quán và khách hàng để họ nhận thông tin)
        //        var notificationPayload = new NotificationListItemResponse
        //        {
        //            Id = Guid.NewGuid(),
        //            BoothId = order.BoothOwnerId,
        //            Type = "ORDER_REFUNDED",
        //            Title = "Đơn hàng đã được hoàn tiền!",
        //            Content = $"Đơn hàng #{order.OrderCode} đã được hoàn tiền thành công. Số tiền hoàn: {order.FinalAmount:N0}đ. Lý do: {reason}",
        //            IsRead = false,
        //            ReferenceType = "Order",
        //            ReferenceId = order.Id,
        //            CreatedAt = DateTime.UtcNow
        //        };

        //        // Báo cho chủ quầy qua SignalR
        //        await _notificationPublisher.PublishAsync(order.BoothOwnerId, notificationPayload, unreadCount: 1);

        //        return ApiResponse<bool>.SuccessResponse(true, "Yêu cầu hoàn tiền đã được xử lý và cập nhật thành công.");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Lỗi xảy ra khi thực hiện hoàn tiền cho đơn hàng #{orderCode}");
        //        return ApiResponse<bool>.Failure($"Lỗi hệ thống khi hoàn tiền: {ex.Message}", data: false);
        //    }
        //}

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
