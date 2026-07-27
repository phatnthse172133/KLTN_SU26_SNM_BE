using ApplicationLayer.Helppers;
using System.ComponentModel.DataAnnotations;
using static DomainLayer.Enums.GeneralEnum;

namespace ApplicationLayer.DTOs.Requests;

public sealed class BoothOwnerOrderQuery : PaginationReq
{
    public string? Keyword { get; set; }
    public OrderStatus? Status { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class UpdateBoothOwnerOrderStatusRequest
{
    [Required]
    public OrderStatus NewStatus { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

public sealed class CreateWalkInOrderRequest
{
    [Required, MinLength(1)]
    public List<CreateWalkInOrderItemRequest> Items { get; set; } = [];

    [StringLength(500)]
    public string? Note { get; set; }

    public PaymentType PaymentMethod { get; set; } = PaymentType.Cash;
}

public sealed class CreateWalkInOrderItemRequest
{
    public Guid FoodItemId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}
