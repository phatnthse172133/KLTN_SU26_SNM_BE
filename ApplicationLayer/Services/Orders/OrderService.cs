using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using ApplicationLayer.Services.Notifications;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PayOS;
using PayOS.Models.V1.Payouts.Batch;
using PayOS.Models.V2.PaymentRequests;
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
        private readonly PayOSClient _payOSClient;
        private readonly IRealtimeNotificationPublisher _notificationPublisher;
        private readonly IFoodItemRepository _foodItemRepo;
        private readonly ILogger<OrderService> _logger;
        private readonly IConfiguration _config;

        public OrderService(IOrderRepository orderRepo, PayOSClient payOSClient, IRealtimeNotificationPublisher notificationPublisher, IFoodItemRepository foodItemRepo, ILogger<OrderService> logger, IConfiguration config)
        {
            _orderRepo = orderRepo;
            _payOSClient = payOSClient;
            _notificationPublisher = notificationPublisher;
            _foodItemRepo = foodItemRepo;
            _config = config;
            _logger = logger;
        }

        //Dành cho customer lẫn khách vang lai (Walk-in) đặt món, trả về link thanh toán nếu chọn online
        public async Task<ApiResponse<OrderResponseDto>> CreateOrderAsync(CreateOrderDto dto)
        {
            // 1. Sinh mã đơn hàng dạng Số nguyên (Duy nhất) vì PayOS ép buộc mã đơn là kiểu long/int
            long uniqueOrderCode = long.Parse(DateTime.UtcNow.ToString("yyMMddHHmmss") + new Random().Next(100, 999)); ;

            // XỬ LÝ KHÁCH HÀNG VÃNG LAI: Nếu chủ quầy đặt hộ và không có CustomerId cụ thể
            Guid? finalCustomerId = dto.CustomerId;
            if (dto.IsCreatedByBooth && finalCustomerId == null)
            {
                // Đọc từ file appsettings.json ra, nếu file config lỗi thì dùng giá trị mặc định để backup
                var walkInIdString = _config["SystemSettings:WalkInCustomerId"]
                                     ?? "00000000-0000-0000-0000-000000000001";
                finalCustomerId = Guid.Parse(walkInIdString);
            }

            // 2. Khởi tạo đối tượng Order chính
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = (Guid) finalCustomerId,
                BoothOwnerId = dto.BoothOwnerId,
                OrderCode = uniqueOrderCode,
                Note = dto.Note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DiscountAmount = dto.DiscountAmount,
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
            order.TotalAmount = calculatedTotalAmount;
            order.FinalAmount = calculatedTotalAmount - dto.DiscountAmount;
            if (order.FinalAmount < 0) order.FinalAmount = 0; // Tránh tiền bị âm

            // 5. Khởi tạo bản ghi lịch sử giao dịch ở bảng Payment
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

            // 6. RẼ NHÁNH LOGIC THANH TOÁN
            CreatePaymentLinkResponse? paymentLink = null;

            if (dto.PaymentMethod == PaymentType.Cash)
            { 
                // Bắn SignalR báo cho App Chủ quầy (BoothOwnerId) biết có đơn tiền mặt mới!
                var notificationPayload = new NotificationListItemResponse
                {
                    Id = Guid.NewGuid(),
                    BoothId = order.BoothOwnerId, // Gán Id của quầy nhận đơn
                    Type = "ORDER_NEW",           // Định nghĩa một mã Type riêng cho đơn mới để FE dễ xử lý logic
                    Title = "Có đơn hàng mới! (Tiền mặt)",
                    Content = $"Bạn có đơn hàng mới #{order.OrderCode} thanh toán bằng tiền mặt. Số tiền: {order.FinalAmount:N0}đ",
                    IsRead = false,
                    ReferenceType = "Order",      // Nói cho FE biết: "Cái ID đi kèm này là của bảng Order"
                    ReferenceId = order.Id,       // Truyền chính xác OrderId sang để FE làm Deep Link nhấn vào là mở đơn hàng
                    CreatedAt = DateTime.UtcNow
                };

                // Bắn đích danh đến phòng của Chủ quán
                await _notificationPublisher.PublishAsync(order.BoothOwnerId, notificationPayload, unreadCount: 1);
            }
            else if (dto.PaymentMethod == PaymentType.PayOS)
            {
                try
                {
                    // Tiến hành gọi API sang hệ thống PayOS để lấy Link mã QR
                    var paymentRequest = new CreatePaymentLinkRequest
                    {
                        OrderCode = uniqueOrderCode,// Truyền mã đơn kiểu long
                        Amount = (int)order.FinalAmount,// Ép về kiểu int theo cấu trúc PayOS
                        Description = $"Process {uniqueOrderCode}",
                        ReturnUrl = "https://your-snm-app/cancel", // Link FE xử lý khi khách thanh toán xong trên web PayOS
                        CancelUrl = "https://your-snm-app/cancel"  // Link FE xử lý khi khách bấm hủy trên web PayOS
                    };

                    paymentLink = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);
                    payment.CheckoutUrl = paymentLink.CheckoutUrl;
                    payment.PaymentLinkId = paymentLink.PaymentLinkId;
                }
                catch (Exception ex)
                {
                    //throw new Exception("Lỗi kết nối cổng thanh toán PayOS: " + ex.Message);
                    return ApiResponse<OrderResponseDto>.Failure(ex.Message);
                }
            }

            // 7. Lưu trọn gói Đơn hàng + Chi tiết đơn + Topping + Lịch sử Payment vào DB (Chỉ 1 lần Save duy nhất)
            await _orderRepo.AddAsync(order);
            await _orderRepo.SaveChangesAsync();

            // 8. Trả kết quả về cho Controller
            return ApiResponse<OrderResponseDto>.SuccessResponse(
                new OrderResponseDto
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    Status = order.Status,
                    PaymentUrl = paymentLink?.CheckoutUrl // Nếu trả tiền mặt thì PaymentUrl = null
                },
                "Đơn hàng đã được tạo thành công!"
            );
        }

        public async Task<bool> ProcessPaymentWebhookAsync(Webhook webhookBody)
        {

            try
            {
                // 1. Gọi hàm VerifyAsync để kiểm tra bảo mật Signature
                // Nếu dữ liệu bị hacker sửa đổi, hàm này sẽ ném ra Exception hoặc thất bại
                WebhookData verifiedData = await _payOSClient.Webhooks.VerifyAsync(webhookBody);
                NotificationListItemResponse? notificationPayload = null;
                if (verifiedData == null)
                {
                    _logger.LogWarning("Webhook nhận được dữ liệu không hợp lệ hoặc chữ ký giả mạo.");
                    return true;
                }

                // 2. Tìm Đơn hàng bằng OrderCode lấy từ verifiedData
                var order = await _orderRepo.GetOrderByCodeAsync(verifiedData.OrderCode);

                if (order == null)
                {
                    _logger.LogWarning($"Không tìm thấy đơn hàng nào khớp với OrderCode: {verifiedData.OrderCode} từ Webhook.");
                    return true;
                }

                // 3. Nếu đơn này đã được xử lý từ trước, trả về true luôn để tránh lặp trùng
                if (order.Status != OrderStatus.Placed)
                {
                    _logger.LogInformation($"Đơn hàng #{order.OrderCode} đã được xử lý trước đó (Trạng thái hiện tại: {order.Status}). Bỏ qua xử lý trùng lặp.");
                    return true;
                }

                // KIỂM TRA CHỐNG HACK TIỀN: So sánh số tiền PayOS nhận được với giá trị đơn hàng
                if (verifiedData.Amount < order.FinalAmount)
                {
                    // Cập nhật status đơn hàng sang Underpaid (thanh toán thiếu)
                    order.Status = OrderStatus.Underpaid;
                    order.UpdatedAt = DateTime.UtcNow;

                    _orderRepo.Update(order);
                    await _orderRepo.SaveChangesAsync();

                    // Bắn tin nhắn báo cho chủ quầy biết có gian lận hoặc lỗi dòng tiền
                    string warningGroup = $"notification:{order.BoothOwnerId}";

                    notificationPayload = new NotificationListItemResponse
                    {
                        Id = Guid.NewGuid(),
                        BoothId = order.BoothOwnerId,
                        Type = "ORDER_UNDERPAID",
                        Title = "Đơn hàng bị trả thiếu tiền!",
                        Content = $"Đơn hàng {order.OrderCode} đối soát phát hiện thanh toán thiếu. Yêu cầu: {order.FinalAmount:N0}đ, Thực nhận: {verifiedData.Amount:N0}đ",
                        IsRead = false,
                        ReferenceType = "Order",
                        ReferenceId = order.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _notificationPublisher.PublishAsync(order.BoothOwnerId, notificationPayload, unreadCount: 1);

                    return true;
                }

                // 4. CẬP NHẬT LỊCH SỬ BẢNG PAYMENT
                var payment = order.Payments.FirstOrDefault(p => p.PaymentLinkId == verifiedData.PaymentLinkId);

                if (payment == null)
                {
                    // Dự phòng nếu không tìm thấy theo ID link, lấy bản ghi Pending mới nhất
                    payment = order.Payments.OrderByDescending(p => p.CreatedAt)
                                            .FirstOrDefault(p => p.Status == PaymentStatus.Pending);
                }

                if (payment != null)
                {
                    payment.Status = PaymentStatus.Paid;
                    payment.GatewayRef = verifiedData.Reference; // Mã đối chiếu ngân hàng
                    payment.PaidAt = DateTime.UtcNow;
                    payment.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _logger.LogWarning($"Cảnh báo: Đơn hàng #{order.OrderCode} thanh toán thành công nhưng không tìm thấy bản ghi Payment tương ứng để cập nhật dòng tiền!");
                }

                // 5. CẬP NHẬT TRẠNG THÁI ĐƠN HÀNG (Tiền vào túi an toàn mới cho phép quầy làm món)
                order.Status = OrderStatus.Preparing;       // Đơn chuyển sang trạng thái hợp lệ để chuẩn bị món
                order.UpdatedAt = DateTime.UtcNow;

                // 6. Lưu xuống DB
                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();

                // 7. Gọi SignalR bắn tin xuống cho BoothOwner
                notificationPayload = new NotificationListItemResponse
                {
                    Id = Guid.NewGuid(),
                    BoothId = order.BoothOwnerId,
                    Type = "ORDER_PAID",          // Type dành cho đơn đã thanh toán online thành công
                    Title = "Đơn hàng đã thanh toán!",
                    Content = $"Đơn hàng #{order.OrderCode} đã được thanh toán thành công qua PayOS. Số tiền: {order.FinalAmount:N0}đ",
                    IsRead = false,
                    ReferenceType = "Order",      // Định danh kiểu tham chiếu
                    ReferenceId = order.Id,       // Id của đơn hàng để FE click vào là xem được luôn
                    CreatedAt = DateTime.UtcNow
                };

                // Bắn đến máy của Chủ quán qua SignalR Group
                await _notificationPublisher.PublishAsync(order.BoothOwnerId, notificationPayload, unreadCount: 1);

                return true;
            }
            catch (Exception ex)
            {
                // Nếu quá trình giải mã VerifyAsync bị lỗi (hacker phá), code vào đây và từ chối xử lý
                _logger.LogError(ex, $"Lỗi nghiêm trọng xảy ra khi xử lý Webhook cho đơn hàng!");
                return false;
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

            if (order.CustomerId != null && order.CustomerId != walkInId)
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
            if (order == null) return ApiResponse<bool>.Failure("Đơn hàng không tồn tại", false);

            // Chỉ cho phép hủy khi đơn đang ở trạng thái chờ thanh toán (Pending)
            
            if (order.Status != OrderStatus.Placed)
            {
                return ApiResponse<bool>.Failure($"Đơn hàng không thể hủy ở trạng thái {order.Status}", false);
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
                    await _payOSClient.PaymentRequests.CancelAsync(order.OrderCode);
                    
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
                return ApiResponse<bool>.Failure($"Lỗi khi đồng bộ hủy đơn với PayOS. Lỗi: {ex.Message}", false);
            }
        }

        //Tình huống khách đặt món payos, trả tiền, nhưng mạng lỗi và webhook không về kịp, khách bấm nút "Tôi đã thanh toán" trên FE để xác nhận, thì gọi API này để kiểm tra trạng thái thực tế từ PayOS
        public async Task<ApiResponse<bool>> ActiveCheckPaymentStatus(long orderCode)
        {
            var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
            if (order == null) return ApiResponse<bool>.Failure("Đơn hàng không tồn tại", false);

            // Nếu đơn đã xử lý thành công trước đó rồi thì thôi
            if (order.Status != OrderStatus.Placed && 
                order.Status != OrderStatus.Underpaid && 
                order.Status != OrderStatus.Cancelled) //order có status từ preparing, ready, completed thì coi như đã thanh toán thành công rồi
                return ApiResponse<bool>.SuccessResponse(true, "Đơn đã được thanh toán và đang xử lý.");

            try
            {
                // 1. CHỦ ĐỘNG GỌI SANG PAYOS ĐỂ KIỂM TRA (Không đợi Webhook)
                var paymentInfo = await _payOSClient.PaymentRequests.GetAsync(orderCode);

                // 2. Nếu bên PayOS báo đã nhận được tiền (PAID)
                if (paymentInfo.Status == PaymentLinkStatus.Paid)
                {
                    if (paymentInfo.AmountPaid < order.FinalAmount)
                    {
                        order.Status = OrderStatus.Underpaid;
                        order.UpdatedAt = DateTime.UtcNow;
                        _orderRepo.Update(order);
                        await _orderRepo.SaveChangesAsync();

                        // Bắn tin nhắn cảnh báo thiếu tiền cho chủ quầy
                        var warningPayload = new NotificationListItemResponse
                        {
                            Id = Guid.NewGuid(),
                            BoothId = order.BoothOwnerId,
                            Type = "ORDER_UNDERPAID",
                            Title = "Đơn hàng bị trả thiếu tiền!",
                            Content = $"Đơn hàng {order.OrderCode} đối soát phát hiện thanh toán thiếu. Yêu cầu: {order.FinalAmount:N0}đ, Thực nhận: {paymentInfo.AmountPaid:N0}đ",
                            IsRead = false,
                            ReferenceType = "Order",
                            ReferenceId = order.Id,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _notificationPublisher.PublishAsync(order.BoothOwnerId, warningPayload, unreadCount: 1);

                        return ApiResponse<bool>.Failure("Bạn đã thanh toán thiếu số tiền yêu cầu. Vui lòng liên hệ quầy để xử lý.", false);
                    }

                    var previousStatus = order.Status;

                    // Thực hiện các bước cập nhật DB và bắn SignalR y hệt như luồng Webhook thành công
                    order.Status = OrderStatus.Preparing;
                    order.UpdatedAt = DateTime.UtcNow;

                    // Tìm và cập nhật payment
                    var payment = order.Payments
                                       .OrderByDescending(p => p.CreatedAt)
                                       .FirstOrDefault(p => p.Status == PaymentStatus.Pending);
                    if (payment != null)
                    {
                        payment.Status = PaymentStatus.Paid;
                        payment.GatewayRef = paymentInfo.Transactions?.FirstOrDefault()?.Reference;
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

                return ApiResponse<bool>.Failure("Thanh toán chưa được ghi nhận trên hệ thống PayOS", false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi xảy ra khi đối soát đơn hàng #{orderCode}");
                return ApiResponse<bool>.Failure($"Lỗi khi đối soát với PayOS: {ex.Message}", false);
            }
        }

        //public async Task<ApiResponse<bool>> RefundOrderAsync(long orderCode, 
        //                                                      string reason, 
        //                                                      string customerBankBin,  //Mã BIN ngân hàng (6 số đầu) của khách để PayOS đối chiếu, nếu có
        //                                                      string customerAccountNumber) //Số tài khoản ngân hàng của khách để PayOS đối chiếu, nếu có
        //{
        //    // 1. Tìm đơn hàng kèm danh sách thanh toán
        //    var order = await _orderRepo.GetOrderByCodeAsync(orderCode);
        //    if (order == null) return ApiResponse<bool>.Failure("Đơn hàng không tồn tại", false);

        //    // 2. Kiểm tra trạng thái đơn hàng: Chỉ cho hoàn tiền khi đơn đã thanh toán thành công và quầy chưa hoàn tất phục vụ món (Chưa giao hàng)
        //    if (order.Status != OrderStatus.Preparing &&
        //        order.Status != OrderStatus.ReadyForPickup &&
        //        order.Status != OrderStatus.Underpaid) // khách trả thiếu cũng cần hoàn
        //    {
        //        return ApiResponse<bool>.Failure($"Đơn hàng ở trạng thái '{order.Status}' không thỏa mãn điều kiện để hoàn tiền.", false);
        //    }

        //    // 3. Tìm bản ghi Payment đã thanh toán thành công (Paid) để hoàn lại
        //    var paidPayment = order.Payments
        //                           .OrderByDescending(p => p.CreatedAt)
        //                           .FirstOrDefault(p => p.Status == PaymentStatus.Paid);

        //    if (paidPayment == null)
        //    {
        //        return ApiResponse<bool>.Failure("Không tìm thấy giao dịch đã thanh toán thành công của đơn hàng này để thực hiện hoàn tiền.", false);
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
        //        return ApiResponse<bool>.Failure($"Lỗi hệ thống khi hoàn tiền: {ex.Message}", false);
        //    }
        //}

    }
}
