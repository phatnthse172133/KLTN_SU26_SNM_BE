using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Responses
{
    public class OrderResponseDto
    {
        public Guid OrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public string Status { get; set; } // "Placed" hoặc "PendingPayment"
        public string? PaymentUrl { get; set; } // Chỉ có nếu chọn thanh toán PayOS
    }
}
