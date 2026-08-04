using DomainLayer.InterfaceRepository;

namespace ApplicationLayer.Services.Menus;

public sealed class FoodAiProfileRebuildService(IFoodItemRepository foods, IFoodAiProfileGenerator generator) : IFoodAiProfileRebuildService
{
    public async Task<bool> RebuildOneAsync(Guid foodItemId, CancellationToken ct = default)
    {
        var food = (await foods.GetSemanticProfileBatchAsync(foodItemId, 1, ct)).SingleOrDefault();
        if (food is null) return false;
        generator.Rebuild(food, DateTime.UtcNow);
        await foods.SaveChangesAsync();
        return true;
    }

    public async Task<int> RebuildBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var batch = await foods.GetSemanticProfileBatchAsync(null, batchSize, ct);
        var now = DateTime.UtcNow;
        foreach (var food in batch) generator.Rebuild(food, now);
        if (batch.Count > 0) await foods.SaveChangesAsync();
        return batch.Count;
    }
}
