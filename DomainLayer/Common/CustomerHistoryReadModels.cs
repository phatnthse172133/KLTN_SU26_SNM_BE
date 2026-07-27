using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Common;

public sealed class CustomerOrderHistoryReadModel
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
}

public sealed class CustomerOrderDetailReadModel
{
    public Guid OrderId { get; set; }
    public long OrderCode { get; set; }
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public OrderStatus OrderStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CustomerOrderItemReadModel> Items { get; set; } = [];
    public List<CustomerPaymentReadModel> Payments { get; set; } = [];
    public CustomerPromotionReadModel? Promotion { get; set; }
}

public sealed class CustomerOrderItemReadModel
{
    public Guid FoodItemId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class CustomerPaymentReadModel
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

public sealed class CustomerPromotionReadModel
{
    public Guid PromotionId { get; set; }
    public string? PromotionCode { get; set; }
    public string? PromotionTitle { get; set; }
    public decimal DiscountAmount { get; set; }
}
