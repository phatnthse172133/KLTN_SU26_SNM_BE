using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using ApplicationLayer.Services.PayOS;
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
        private readonly IPayOSService _payos;
        private readonly IRealtimeNotificationPublisher _notificationPublisher;
        private readonly IFoodItemRepository _foodItemRepo;
        private readonly ILogger<OrderService> _logger;
        private readonly IConfiguration _config;
        private readonly IPayOSOrderCodeGenerator _orderCodeGenerator;
        private readonly IBoothRepository _boothRepo;

        public OrderService(IOrderRepository orderRepo, IPayOSService payos, IRealtimeNotificationPublisher notificationPublisher, IFoodItemRepository foodItemRepo, ILogger<OrderService> logger, IConfiguration config, IPayOSOrderCodeGenerator orderCodeGenerator, IBoothRepository boothRepo)
        {
            _orderRepo = orderRepo;
            _payos = payos;
            _notificationPublisher = notificationPublisher;
            _foodItemRepo = foodItemRepo;
            _config = config;
            _logger = logger;
            _orderCodeGenerator = orderCodeGenerator;
            _boothRepo = boothRepo;
        }

        //DÃ nh cho customer láº«n khÃ¡ch vang lai (Walk-in) Ä‘áº·t mÃ³n, tráº£ vá» link thanh toÃ¡n náº¿u chá»n online
        public async Task<ApiResponse<OrderResponseDto>> CreateOrderAsync(CreateOrderDto dto)
        {
            // 0. Guard: reject orders for booths in deleted markets
            var booth = await _boothRepo.GetByOwnerIdAsync(dto.BoothOwnerId);
            if (booth is null)
                return ApiResponse<OrderResponseDto>.Failure("Booth not found.", "BOOTH_NOT_FOUND");
            if (booth.NightMarket is null || booth.NightMarket.IsDeleted)
                return ApiResponse<OrderResponseDto>.Failure("This night market is no longer available. Orders cannot be placed.", "MARKET_UNAVAILABLE");

            // 1. Sinh mÃ£ Ä‘Æ¡n hÃ ng dáº¡ng Sá»‘ nguyÃªn (Duy nháº¥t) vÃ¬ PayOS Ã©p buá»™c mÃ£ Ä‘Æ¡n lÃ  kiá»ƒu long/int
            long uniqueOrderCode = await _orderCodeGenerator.GenerateAsync(PayOSOrderSource.Order);

            // Xá»¬ LÃ KHÃCH HÃ€NG VÃƒNG LAI: Náº¿u chá»§ quáº§y Ä‘áº·t há»™ vÃ  khÃ´ng cÃ³ CustomerId cá»¥ thá»ƒ
            Guid? finalCustomerId = dto.CustomerId;
            if (dto.IsCreatedByBooth && finalCustomerId == null)
            {
                // Äá»c tá»« file appsettings.json ra, náº¿u file config lá»—i thÃ¬ dÃ¹ng giÃ¡ trá»‹ máº·c Ä‘á»‹nh Ä‘á»ƒ backup
                var walkInIdString = _config["SystemSettings:WalkInCustomerId"]
                                     ?? "00000000-0000-0000-0000-000000000001";
                finalCustomerId = Guid.Parse(walkInIdString);
            }

            if (!finalCustomerId.HasValue)
                return ApiResponse<OrderResponseDto>.Failure("CustomerId is required for orders created by customers.", "CUSTOMER_ID_REQUIRED");

            // 2. Khá»Ÿi táº¡o Ä‘á»‘i tÆ°á»£ng Order chÃ­nh
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = finalCustomerId.Value,
                BoothOwnerId = dto.BoothOwnerId,
                OrderCode = uniqueOrderCode,
                Note = dto.Note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DiscountAmount = dto.DiscountAmount,
                Status = OrderStatus.Placed
            };

            // 3. Duyá»‡t danh sÃ¡ch mÃ³n Äƒn + Topping Ä‘á»ƒ lÆ°u chi tiáº¿t vÃ  tÃ­nh tá»•ng tiá»n thá»±c táº¿
            decimal calculatedTotalAmount = 0;

            //REAL
            var foodIds = dto.Items.Select(i => i.FoodItemId).ToList();
            var foodItemsFromDb = await _foodItemRepo.GetAllFoodItemsByIdsAsync(foodIds);

            //TEST (táº¡m thá»i comment 2 dÃ²ng trÃªn vÃ  4 dÃ²ng dÆ°á»›i Ä‘á»ƒ test PayOS, trÃ¡nh lá»—i null ref khi chÆ°a cÃ³ mÃ³n Äƒn thá»±c táº¿ trong DB)


            foreach (var itemDto in dto.Items)
            {
                var dbFoodItem = foodItemsFromDb.FirstOrDefault(f => f.Id == itemDto.FoodItemId);
                if (dbFoodItem == null) return ApiResponse<OrderResponseDto>.Failure("MÃ³n Äƒn khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ bá»‹ xÃ³a khá»i thá»±c Ä‘Æ¡n!");

                decimal realUnitPrice = dbFoodItem.Price;
                decimal itemTotalPrice = realUnitPrice * itemDto.Quantity;
                //decimal realUnitPrice = itemDto.UnitPrice;
                //decimal itemTotalPrice = realUnitPrice * itemDto.Quantity;

                var orderDetail = new OrderDetail
                {
                    Id = Guid.NewGuid(),
                    FoodItemId = itemDto.FoodItemId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = realUnitPrice, // Snapshot giÃ¡ mÃ³n
                    TotalPrice = itemTotalPrice,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Xá»­ lÃ½ Ä‘á»‘ng Topping Ä‘i kÃ¨m cá»§a mÃ³n Äƒn Ä‘Ã³ (Náº¿u cÃ³)
                //foreach (var toppingDto in itemDto.Toppings)
                //{
                //    var orderDetailTopping = new OrderDetailTopping
                //    {
                //        Id = Guid.NewGuid(),
                //        ToppingItemId = toppingDto.ToppingItemId,
                //        ToppingName = toppingDto.ToppingName,
                //        UnitPrice = toppingDto.UnitPrice, // Snapshot giÃ¡ topping
                //    };

                //    orderDetail.OrderDetailToppings.Add(orderDetailTopping);

                //    // Cá»™ng dá»“n tiá»n Topping vÃ o tá»•ng tiá»n cá»§a mÃ³n (NhÃ¢n vá»›i sá»‘ lÆ°á»£ng mÃ³n Äƒn Ä‘áº·t)
                //    itemTotalPrice += (toppingDto.UnitPrice * itemDto.Quantity);
                //}

                // Cáº­p nháº­t láº¡i chÃ­nh xÃ¡c TotalPrice cá»§a OrderDetail sau khi cÃ³ topping
                //orderDetail.TotalPrice = itemTotalPrice;

                // Cá»™ng vÃ o tá»•ng tiá»n lá»›n cá»§a cáº£ ÄÆ¡n hÃ ng
                calculatedTotalAmount += itemTotalPrice;

                // Add vÃ o Collection cÃ³ sáºµn trong Entity Order
                order.OrderDetails.Add(orderDetail);
            }

            // 4. Ãp Ä‘áº·t sá»‘ tiá»n cuá»‘i cÃ¹ng cho ÄÆ¡n hÃ ng
            order.TotalAmount = calculatedTotalAmount;
            order.FinalAmount = calculatedTotalAmount - dto.DiscountAmount;
            if (order.FinalAmount < 0) order.FinalAmount = 0; // TrÃ¡nh tiá»n bá»‹ Ã¢m

            // 5. Khá»Ÿi táº¡o báº£n ghi lá»‹ch sá»­ giao dá»‹ch á»Ÿ báº£ng Payment
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                BoothOwnerId = dto.BoothOwnerId,
                Amount = order.FinalAmount,
                Type = dto.PaymentMethod,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            order.Payments.Add(payment);

            // 6. Ráº¼ NHÃNH LOGIC THANH TOÃN
            string? checkoutUrl = null;

            if (dto.PaymentMethod == PaymentType.Cash)
            {
                // Báº¯n SignalR bÃ¡o cho App Chá»§ quáº§y (BoothOwnerId) biáº¿t cÃ³ Ä‘Æ¡n tiá»n máº·t má»›i!
                var notificationPayload = new NotificationListItemResponse
                {
                    Id = Guid.NewGuid(),
                    BoothId = order.BoothOwnerId, // GÃ¡n Id cá»§a quáº§y nháº­n Ä‘Æ¡n
                    Type = "ORDER_NEW",           // Äá»‹nh nghÄ©a má»™t mÃ£ Type riÃªng cho Ä‘Æ¡n má»›i Ä‘á»ƒ FE dá»… xá»­ lÃ½ logic
                    Title = "CÃ³ Ä‘Æ¡n hÃ ng má»›i! (Tiá»n máº·t)",
                    Content = $"Báº¡n cÃ³ Ä‘Æ¡n hÃ ng má»›i #{order.OrderCode} thanh toÃ¡n báº±ng tiá»n máº·t. Sá»‘ tiá»n: {order.FinalAmount:N0}Ä‘",
                    IsRead = false,
                    ReferenceType = "Order",      // NÃ³i cho FE biáº¿t: "CÃ¡i ID Ä‘i kÃ¨m nÃ y lÃ  cá»§a báº£ng Order"
                    ReferenceId = order.Id,       // Truyá»n chÃ­nh xÃ¡c OrderId sang Ä‘á»ƒ FE lÃ m Deep Link nháº¥n vÃ o lÃ  má»Ÿ Ä‘Æ¡n hÃ ng
                    CreatedAt = DateTime.UtcNow
                };

                // Báº¯n Ä‘Ã­ch danh Ä‘áº¿n phÃ²ng cá»§a Chá»§ quÃ¡n
                await _notificationPublisher.PublishAsync(order.BoothOwnerId, notificationPayload, unreadCount: 1);
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

            // 7. LÆ°u trá»n gÃ³i ÄÆ¡n hÃ ng + Chi tiáº¿t Ä‘Æ¡n + Topping + Lá»‹ch sá»­ Payment vÃ o DB (Chá»‰ 1 láº§n Save duy nháº¥t)
            await _orderRepo.AddAsync(order);
            await _orderRepo.SaveChangesAsync();

            // 8. Tráº£ káº¿t quáº£ vá» cho Controller
            return ApiResponse<OrderResponseDto>.SuccessResponse(
                new OrderResponseDto
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    Status = order.Status,
                    PaymentUrl = checkoutUrl
                },
                "ÄÆ¡n hÃ ng Ä‘Ã£ Ä‘Æ°á»£c táº¡o thÃ nh cÃ´ng!"
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

                // Check for existing pending PayOS payment â€” return its link if still valid
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
                            // Fully paid â€” transition to Preparing
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

                // order.Status == Placed â€” first webhook
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
                    // Atomic conditional update: only transitions Placed â†’ Preparing
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

                    // Báº¯n Ä‘Ã­ch danh vÃ o Group SignalR cá»§a khÃ¡ch hÃ ng (TÃªn group chÃ­nh lÃ  CustomerId)
                    await _notificationPublisher.PublishAsync(order.CustomerId, customerNotification, unreadCount: 1);
                }
            }

            return ApiResponse<bool>.SuccessResponse(true, "Cáº­p nháº­t tráº¡ng thÃ¡i Ä‘Æ¡n hÃ ng thÃ nh cÃ´ng!");
        }

        //KhÃ¡ch chá»§ Ä‘á»™ng há»§y Ä‘Æ¡n hÃ ng trÆ°á»›c khi quáº§y nháº­n Ä‘Æ¡n (Chá»‰ Ã¡p dá»¥ng cho khÃ¡ch Ä‘áº·t qua App, khÃ´ng Ã¡p dá»¥ng cho khÃ¡ch vÃ£ng lai)
        public async Task<ApiResponse<bool>> CancelOrder(long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order == null) return ApiResponse<bool>.Failure("ÄÆ¡n hÃ ng khÃ´ng tá»“n táº¡i", data: false);

            // Chá»‰ cho phÃ©p há»§y khi Ä‘Æ¡n Ä‘ang á»Ÿ tráº¡ng thÃ¡i chá» thanh toÃ¡n (Pending)

            if (order.Status != OrderStatus.Placed)
            {
                return ApiResponse<bool>.Failure($"ÄÆ¡n hÃ ng khÃ´ng thá»ƒ há»§y á»Ÿ tráº¡ng thÃ¡i {order.Status}", data: false);
            }

            var payment = order.Payments
                               .OrderByDescending(p => p.CreatedAt)
                               .FirstOrDefault(p => p.Status == PaymentStatus.Pending); // Vá»«a lá»c Pending vá»«a láº¥y cÃ¡i Ä‘áº§u tiÃªn

            try
            {
                // 1. Gá»ŒI SANG PAYOS Äá»‚ Há»¦Y LINK THANH TOÃN (Cháº·n khÃ´ng cho quÃ©t QR ná»¯a)
                // HÃ m nÃ y báº¯t buá»™c truyá»n OrderCode (kiá»ƒu long/int) vÃ  lÃ½ do há»§y tÃ¹y Ã½
                if (payment != null)
                {
                    await _payos.CancelPaymentLinkAsync(order.OrderCode);

                    // 2. Cáº­p nháº­t Database
                    payment.Status = PaymentStatus.Cancelled;
                    payment.UpdatedAt = DateTime.UtcNow;
                }

                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();

                return ApiResponse<bool>.SuccessResponse(true, "Há»§y Ä‘Æ¡n hÃ ng thÃ nh cÃ´ng");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lá»—i xáº£y ra khi há»§y Ä‘Æ¡n hÃ ng #{orderCode}");
                return ApiResponse<bool>.Failure("Lá»—i khi Ä‘á»“ng bá»™ há»§y Ä‘Æ¡n vá»›i PayOS. Vui lÃ²ng thá»­ láº¡i sau.", "CANCEL_ORDER_PAYOS_FAILED", data: false);
            }
        }

        //TÃ¬nh huá»‘ng khÃ¡ch Ä‘áº·t mÃ³n payos, tráº£ tiá»n, nhÆ°ng máº¡ng lá»—i vÃ  webhook khÃ´ng vá» ká»‹p, khÃ¡ch báº¥m nÃºt "TÃ´i Ä‘Ã£ thanh toÃ¡n" trÃªn FE Ä‘á»ƒ xÃ¡c nháº­n, thÃ¬ gá»i API nÃ y Ä‘á»ƒ kiá»ƒm tra tráº¡ng thÃ¡i thá»±c táº¿ tá»« PayOS
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

                    // 4. Chuáº©n bá»‹ ná»™i dung thÃ´ng bÃ¡o SignalR
                    string notificationTitle = previousStatus == OrderStatus.Cancelled
                        ? "ÄÆ¡n Ä‘Ã£ há»§y Ä‘Æ°á»£c thanh toÃ¡n trá»…!"
                        : "ÄÆ¡n hÃ ng Ä‘Ã£ thanh toÃ¡n!";

                    string notificationContent = previousStatus == OrderStatus.Cancelled
                        ? $"ÄÆ¡n hÃ ng #{order.OrderCode} (tá»«ng bá»‹ há»§y do quÃ¡ háº¡n) vá»«a Ä‘Æ°á»£c Ä‘á»‘i soÃ¡t thanh toÃ¡n thÃ nh cÃ´ng qua PayOS. Sá»‘ tiá»n: {order.FinalAmount:N0}Ä‘"
                        : $"ÄÆ¡n hÃ ng #{order.OrderCode} Ä‘Ã£ Ä‘Æ°á»£c thanh toÃ¡n thÃ nh cÃ´ng qua PayOS. Sá»‘ tiá»n: {order.FinalAmount:N0}Ä‘";

                    // Báº¯n SignalR bÃ¡o cho chá»§ quáº§y "Ting Ting"
                    var notificationPayload = new NotificationListItemResponse
                    {
                        Id = Guid.NewGuid(),
                        BoothId = order.BoothOwnerId,
                        Type = "ORDER_PAID",          // Type dÃ nh cho Ä‘Æ¡n Ä‘Ã£ thanh toÃ¡n online thÃ nh cÃ´ng
                        Title = notificationTitle,
                        Content = notificationContent,
                        IsRead = false,
                        ReferenceType = "Order",      // Äá»‹nh danh kiá»ƒu tham chiáº¿u
                        ReferenceId = order.Id,       // Id cá»§a Ä‘Æ¡n hÃ ng Ä‘á»ƒ FE click vÃ o lÃ  xem Ä‘Æ°á»£c luÃ´n
                        CreatedAt = DateTime.UtcNow
                    };

                    await _notificationPublisher.PublishAsync(order.BoothOwnerId, notificationPayload, unreadCount: 1);

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

        public async Task<bool> HasOrderWithCodeAsync(long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            return order != null;
        }

    }
}
