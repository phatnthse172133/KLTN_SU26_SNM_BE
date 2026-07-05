using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using PayOS;
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

        public OrderService(IOrderRepository orderRepo, PayOSClient payOSClient)
        {
            _orderRepo = orderRepo;
            _payOSClient = payOSClient;
        }

        public async Task<ApiResponse<OrderResponseDto>> CreateOrderAsync(CreateOrderDto dto)
        {
            // 1. Sinh mã đơn hàng dạng Số nguyên (Duy nhất) vì PayOS ép buộc mã đơn là kiểu long/int
            long uniqueOrderCode = DateTime.UtcNow.Ticks;

            // 2. Khởi tạo đối tượng Order chính
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = dto.CustomerId,
                BoothOwnerId = dto.BoothOwnerId,
                OrderCode = uniqueOrderCode.ToString(),
                Note = dto.Note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                DiscountAmount = dto.DiscountAmount
            };

            // 3. Duyệt danh sách món ăn + Topping để lưu chi tiết và tính tổng tiền thực tế
            decimal calculatedTotalAmount = 0;

            foreach (var itemDto in dto.Items)
            {
                decimal itemTotalPrice = itemDto.UnitPrice * itemDto.Quantity;

                var orderDetail = new OrderDetail
                {
                    Id = Guid.NewGuid(),
                    FoodItemId = itemDto.FoodItemId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = itemDto.UnitPrice, // Snapshot giá món
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
                BoothOwnerId = dto.BoothOwnerId,
                Amount = order.FinalAmount,
                Currency = "VND",
                Gateway = dto.PaymentMethod,
                Type = dto.PaymentMethod == "Cash" ? PaymentType.Cash : PaymentType.PayOS,
                Status = PaymentStatus.Pending, // Mặc định cả 2 đều chờ thu tiền
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            order.Payments.Add(payment);

            // 6. RẼ NHÁNH LOGIC THANH TOÁN (Trọng tâm bài toán)
            CreatePaymentLinkResponse? paymentLink = null;

            if (dto.PaymentMethod == "Cash")
            {
                // Nếu trả tiền mặt -> Đơn hàng có hiệu lực ngay lập tức
                order.Status = OrderStatus.Placed;
                order.PayStatus = PayOrderStatus.Pending;

                // TODO: Bắn SignalR tại đây báo cho App Chủ quầy (BoothOwnerId) biết có đơn tiền mặt mới!
            }
            else if (dto.PaymentMethod == "PayOS")
            {
                // Nếu trả Online -> Đơn hàng treo ở trạng thái chờ quét mã
                order.Status = OrderStatus.PendingPayment;
                order.PayStatus = PayOrderStatus.Pending;

                try
                {
                    // Tiến hành gọi API sang hệ thống PayOS để lấy Link mã QR
                    var paymentRequest = new CreatePaymentLinkRequest
                    {
                        OrderCode = uniqueOrderCode,// Truyền mã đơn kiểu long
                        Amount = (int)order.FinalAmount,// Ép về kiểu int theo cấu trúc PayOS
                        Description = $"Thanh toan SNM #{order.Id.ToString().Substring(0, 6)}",
                        ReturnUrl = "https://your-snm-app/success", // Link FE xử lý khi khách thanh toán xong trên web PayOS
                        CancelUrl = "https://your-snm-app/cancel"  // Link FE xử lý khi khách bấm hủy trên web PayOS
                    };

                    paymentLink = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);
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

            // 8. Trả kết quả gọn gàng về cho Controller
            return ApiResponse<OrderResponseDto>.SuccessResponse(
                new OrderResponseDto
                {
                    OrderId = order.Id,
                    OrderCode = order.OrderCode,
                    Status = order.Status.ToString(),
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

                if (verifiedData == null)
                {
                    return false;
                }

                // 2. Tìm Đơn hàng bằng OrderCode lấy từ verifiedData (Mã kiểu long sang string)
                string orderCodeStr = verifiedData.OrderCode.ToString();
                var order = await _orderRepo.GetOrderByCodeAsync(orderCodeStr);

                if (order == null)
                {
                    return false;
                }

                // 3. Nếu đơn này đã được xử lý từ trước, trả về true luôn để tránh lặp trùng
                if (order.PayStatus == PayOrderStatus.Paid)
                {
                    return true;
                }

                // 4. CẬP NHẬT TRẠNG THÁI ĐƠN HÀNG 
                order.Status = OrderStatus.Placed;       // Đơn chuyển sang trạng thái hợp lệ để chuẩn bị món
                order.PayStatus = PayOrderStatus.Paid;   // Đơn đánh dấu đã thanh toán thành công
                order.UpdatedAt = DateTime.UtcNow;

                // 5. CẬP NHẬT LỊCH SỬ BẢNG PAYMENT
                var payment = order.Payments.FirstOrDefault(p => p.Status == PaymentStatus.Pending);
                if (payment != null)
                {
                    payment.Status = PaymentStatus.Paid;
                    payment.GatewayRef = verifiedData.Reference; // Mã đối chiếu ngân hàng (chữ R viết hoa)
                    payment.PaidAt = DateTime.UtcNow;
                    payment.UpdatedAt = DateTime.UtcNow;
                }

                // 6. Lưu xuống DB
                _orderRepo.Update(order);
                await _orderRepo.SaveChangesAsync();

                // 7. TODO: Gọi SignalR bắn tin xuống cho BoothOwner tại đây!

                return true;
            }
            catch (Exception)
            {
                // Nếu quá trình giải mã VerifyAsync bị lỗi (hacker phá), code nhảy vào đây và từ chối xử lý
                return false;
            }
        }
    }
}
