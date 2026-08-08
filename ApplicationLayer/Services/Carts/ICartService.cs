using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.DTOs.Responses;
using ApplicationLayer.Helppers;

namespace ApplicationLayer.Services.Carts;

public interface ICartService
{
    Task<ApiResponse<CartResponse>> GetCurrentAsync(Guid customerId, PaginationReq pagination, CancellationToken cancellationToken = default);

    Task<ApiResponse<CartItemResponse>> AddItemAsync(Guid customerId, AddCartItemRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<CartBatchAddResponse>> AddItemsAsync(Guid customerId, AddCartItemsRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<CartItemResponse>> UpdateQuantityAsync(Guid customerId, Guid cartItemId, UpdateCartItemQuantityRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> RemoveItemAsync(Guid customerId, Guid cartItemId, CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> RemoveBoothItemsAsync(Guid customerId, Guid boothId, CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> ClearAsync(Guid customerId, CancellationToken cancellationToken = default);
}
