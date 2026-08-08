namespace ApplicationLayer.Services.Menus;

public interface IFoodAiProfileRebuildService
{
    Task<bool> RebuildOneAsync(Guid foodItemId, CancellationToken cancellationToken = default);
    Task<int> RebuildBatchAsync(int batchSize, CancellationToken cancellationToken = default);
}
