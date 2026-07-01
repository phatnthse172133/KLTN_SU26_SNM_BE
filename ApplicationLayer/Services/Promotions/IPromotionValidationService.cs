using ApplicationLayer.DTOs.Responses;
using DomainLayer.Entities;

namespace ApplicationLayer.Services.Promotions;

public interface IPromotionValidationService
{
    Task<PromotionValidationResponse> ValidateAsync(Guid customerId, Promotion promotion, IReadOnlyCollection<CartItem> boothItems, CancellationToken cancellationToken = default);
}
