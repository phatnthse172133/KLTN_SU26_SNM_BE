using ApplicationLayer.DTOs.Responses;

namespace ApplicationLayer.Services.MarketLayouts;

public interface ILayoutGraphValidationService
{
    Task<MarketLayoutValidationResponse> ValidateAsync(Guid layoutId, CancellationToken cancellationToken = default);
}
