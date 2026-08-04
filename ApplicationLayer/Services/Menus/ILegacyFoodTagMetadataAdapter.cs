using DomainLayer.Entities;

namespace ApplicationLayer.Services.Menus;

public interface ILegacyFoodTagMetadataAdapter
{
    Task ApplySupportedAsync(FoodItem food, IReadOnlyCollection<FoodTag> tags, DateTime utcNow, CancellationToken cancellationToken = default);
}
