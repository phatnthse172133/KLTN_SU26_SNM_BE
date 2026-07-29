using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;
using System.Text.Json.Serialization;
using ApplicationLayer.Serialization;

namespace ApplicationLayer.DTOs.Responses
{
    public class OrderResponseDto
    {
        public Guid OrderId { get; set; }
        public long OrderCode { get; set; }
        [JsonPropertyName("orderStatus")]
        [JsonConverter(typeof(UpperSnakeCaseEnumConverter<OrderStatus>))]
        public OrderStatus Status { get; set; }
        public string? PaymentUrl { get; set; } // Chỉ có nếu chọn thanh toán PayOS
        public Guid? PaymentId { get; set; }
        [JsonConverter(typeof(UpperSnakeCaseEnumConverter<PaymentType>))]
        public PaymentType PaymentMethod { get; set; }
        [JsonConverter(typeof(UpperSnakeCaseEnumConverter<PaymentStatus>))]
        public PaymentStatus PaymentStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public sealed class CheckoutPreviewResponse
    {
        public Guid CartId { get; set; }
        public IReadOnlyCollection<CheckoutBoothGroupResponse> BoothGroups { get; set; } = [];
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal FinalAmount { get; set; }
        public IReadOnlyCollection<EligiblePromotionResponse> EligiblePromotions { get; set; } = [];
        public IReadOnlyCollection<string> AvailablePaymentMethods { get; set; } = ["CASH", "PAYOS"];
    }

    public sealed class CheckoutBoothGroupResponse
    {
        public Guid BoothId { get; set; }
        public string BoothName { get; set; } = string.Empty;
        public IReadOnlyCollection<CheckoutItemResponse> Items { get; set; } = [];
        public decimal Subtotal { get; set; }
    }

    public sealed class CheckoutItemResponse
    {
        public Guid FoodId { get; set; }
        public string FoodName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public sealed class EligiblePromotionResponse
    {
        public Guid PromotionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
    }

    public sealed class CustomerPaymentStatusResponse
    {
        public Guid OrderId { get; set; }
        public long OrderCode { get; set; }
        [JsonConverter(typeof(UpperSnakeCaseEnumConverter<OrderStatus>))]
        public OrderStatus OrderStatus { get; set; }
        [JsonConverter(typeof(UpperSnakeCaseEnumConverter<PaymentStatus>))]
        public PaymentStatus PaymentStatus { get; set; }
        public decimal Amount { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? CheckoutUrl { get; set; }
        public DateTime? ExpiresAt { get; set; }
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
