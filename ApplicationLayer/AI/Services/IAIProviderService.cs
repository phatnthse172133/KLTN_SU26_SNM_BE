using ApplicationLayer.AI.DTOs;

namespace ApplicationLayer.AI.Services;

public interface IAIProviderService
{
    Task<FoodIntentDto> ParseFoodIntentAsync(
        string? userQuery,
        IReadOnlyCollection<string> allowedTags,
        CancellationToken cancellationToken = default);

    Task<string> GenerateExplanationAsync(
        ExplanationContextDto context,
        CancellationToken cancellationToken = default);
}
