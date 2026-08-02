using System.ComponentModel.DataAnnotations;

namespace ApplicationLayer.DTOs.Requests;

public class AddCartItemRequest
{
    public Guid FoodItemId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class UpdateCartItemQuantityRequest
{
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public sealed class AddCartItemsRequest
{
    public IReadOnlyCollection<AddCartItemRequest> Items { get; set; } = [];
}
