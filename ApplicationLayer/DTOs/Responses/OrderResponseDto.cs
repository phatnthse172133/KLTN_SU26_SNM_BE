using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Responses
{
    public class OrderResponseDto
    {
        public Guid OrderId { get; set; }
        public long OrderCode { get; set; }
        public OrderStatus Status { get; set; } // "Placed" hoặc "PendingPayment"
        public string? PaymentUrl { get; set; } // Chỉ có nếu chọn thanh toán PayOS
    }

    public class UpdateOrderStatusDto
    {
        public long OrderCode { get; set; }
        public OrderStatus NewStatus { get; set; } // Preparing -> Ready -> ReadyForPickup -> Completed
    }

    public class SupplementalPaymentResponseDto
    {
        public Guid OrderId { get; set; }
        public long OrderCode { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal FinalAmount { get; set; }
        public string? PaymentUrl { get; set; }
        public long PayOSOrderCode { get; set; }
    }
}
