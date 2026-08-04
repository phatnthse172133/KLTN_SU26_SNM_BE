using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Responses;

public class BoothOwnerOrderListItemResponse
{
    public long OrderCode { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public bool IsWalkInCustomer { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public PaymentType? PaymentMethod { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class BoothOwnerOrderDetailResponse : BoothOwnerOrderListItemResponse
{
    public string? Note { get; set; }
    public IReadOnlyCollection<BoothOwnerOrderItemResponse> Items { get; set; } = [];
}

public sealed class BoothOwnerOrderItemResponse
{
    public string FoodItemName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
