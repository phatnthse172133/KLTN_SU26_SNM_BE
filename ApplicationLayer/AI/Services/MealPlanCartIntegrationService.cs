using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Services.Carts;

namespace ApplicationLayer.AI.V2.Services;

public sealed class MealPlanCartIntegrationService(ICartService carts) : IMealPlanCartIntegrationService
{
    public async Task<CartBatchAddResponse> AddItemsAsync(
        Guid customerId,
        IReadOnlyCollection<(Guid FoodItemId, int Quantity)> items,
        CancellationToken cancellationToken)
    {
        var response = await carts.AddItemsAsync(customerId, new AddCartItemsRequest
        {
            Items = items.Select(item => new AddCartItemRequest
            {
                FoodItemId = item.FoodItemId,
                Quantity = item.Quantity
            }).ToArray()
        }, cancellationToken);
        return response.Data!;
    }
}
