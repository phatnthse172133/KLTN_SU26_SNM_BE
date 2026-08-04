using ApplicationLayer.Helppers;

namespace ApplicationLayer.DTOs.Responses;

public class CartResponse
{
    public Guid CartId { get; set; }
    public long TotalItemCount { get; set; }
    public PaginationResp<CartBoothResponse> Booths { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public bool CanCheckout { get; set; }
}

public class CartBoothResponse
{
    public Guid BoothId { get; set; }
    public string BoothName { get; set; } = string.Empty;
    public IReadOnlyCollection<CartCategoryResponse> Categories { get; set; } = [];
    public decimal Subtotal { get; set; }
}

public class CartCategoryResponse
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public IReadOnlyCollection<CartItemResponse> Items { get; set; } = [];
}

public class CartItemResponse
{
    public Guid CartItemId { get; set; }
    public Guid FoodItemId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int Quantity { get; set; }
    public decimal CurrentUnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool IsAvailable { get; set; }
    public bool CanOrder { get; set; }
    public string? ReasonCode { get; set; }
}

public sealed class CartBatchAddResponse
{
    public CartResponse Cart { get; set; } = new();
    public IReadOnlyCollection<Guid> AddedFoodItemIds { get; set; } = [];
    public IReadOnlyCollection<Guid> MergedFoodItemIds { get; set; } = [];
}
