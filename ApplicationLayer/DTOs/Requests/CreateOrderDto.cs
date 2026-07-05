using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.DTOs.Requests
{
    public class CreateOrderDto
    {
        public Guid CustomerId { get; set; }
        public Guid BoothOwnerId { get; set; }
        public string? Note { get; set; }
        public string PaymentMethod { get; set; } // "Cash" hoặc "PayOS"
        public decimal DiscountAmount { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
    }
}
