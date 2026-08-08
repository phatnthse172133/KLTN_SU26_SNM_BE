using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Responses;

public sealed class CustomerOrderHistoryResponse
{
    public Guid OrderId { get; set; }
    public long OrderCode { get; set; }
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public OrderStatus OrderStatus { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public decimal FinalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
    public bool CanReview { get; set; }
    public bool HasReview { get; set; }
    public Guid? ReviewId { get; set; }
    public bool CanEditReview { get; set; }
    public DateTime? EditDeadline { get; set; }
    public bool CanComplain { get; set; }
    public Guid? ActiveComplaintId { get; set; }
}

public sealed class CustomerOrderDetailResponse
{
    public Guid OrderId { get; set; }
    public long OrderCode { get; set; }
    public CustomerOrderBoothResponse Booth { get; set; } = new();
    public IReadOnlyCollection<CustomerOrderItemResponse> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public CustomerOrderPromotionResponse? Promotion { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public OrderStatus OrderStatus { get; set; }
    public IReadOnlyCollection<CustomerOrderPaymentResponse> Payments { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool CanReview { get; set; }
    public bool HasReview { get; set; }
    public Guid? ReviewId { get; set; }
    public bool CanEditReview { get; set; }
    public DateTime? EditDeadline { get; set; }
    public bool CanComplain { get; set; }
    public Guid? ActiveComplaintId { get; set; }
}

public sealed class CustomerOrderBoothResponse
{
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
}

public sealed class CustomerOrderItemResponse
{
    public Guid OrderDetailId { get; set; }
    public Guid FoodItemId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class CustomerOrderPromotionResponse
{
    public Guid PromotionId { get; set; }
    public string? PromotionCode { get; set; }
    public string? PromotionTitle { get; set; }
    public decimal DiscountAmount { get; set; }
}

public sealed class CustomerOrderPaymentResponse
{
    public Guid PaymentId { get; set; }
    public PaymentType Type { get; set; }
    public PaymentGateway Gateway { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal Amount { get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundRequestedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
