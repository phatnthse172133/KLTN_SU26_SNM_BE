using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests
{
    public class CreateOrderDto
    {
        public Guid? CustomerId { get; set; }
        public Guid BoothOwnerId { get; set; }
        public Guid BoothId { get; set; }
        public string? Note { get; set; }
        public PaymentType PaymentMethod { get; set; } // "Cash" hoặc "PayOS", đối với khách vãng lai thì mặc định là "Cash"
        public string? PromotionCode { get; set; }
        public List<CartItemDto> Items { get; set; } = new();

        public bool IsCreatedByBooth { get; set; } = false; //Mặc định customer đặt trên app, nếu booth tạo thì set true
    }
}
