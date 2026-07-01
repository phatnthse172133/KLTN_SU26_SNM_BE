using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class PromotionRepository : GenericRepository<Promotion>, IPromotionRepository
{
    public PromotionRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<Promotion>> GetPagedAsync(
        Guid? boothId,
        string? keyword,
        PromotionStatus? status,
        PromotionScope? scope,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DetailsQuery().AsNoTracking();

        if (boothId.HasValue)
            query = query.Where(promotion => promotion.BoothId == boothId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim().ToLower();
            query = query.Where(promotion =>
                promotion.Title.ToLower().Contains(normalizedKeyword)
                || (promotion.PromotionCode != null
                    && promotion.PromotionCode.ToLower().Contains(normalizedKeyword)));
        }

        if (status.HasValue)
        {
            var now = DateTime.UtcNow;
            query = status.Value switch
            {
                PromotionStatus.Active => query.Where(promotion =>
                    (promotion.Status == PromotionStatus.Active
                        || promotion.Status == PromotionStatus.Scheduled)
                    && promotion.StartDate <= now
                    && promotion.EndDate >= now),
                PromotionStatus.Scheduled => query.Where(promotion =>
                    (promotion.Status == PromotionStatus.Active
                        || promotion.Status == PromotionStatus.Scheduled)
                    && promotion.StartDate > now),
                PromotionStatus.Expired => query.Where(promotion =>
                    promotion.Status == PromotionStatus.Expired
                    || ((promotion.Status == PromotionStatus.Active
                            || promotion.Status == PromotionStatus.Scheduled)
                        && promotion.EndDate < now)),
                _ => query.Where(promotion => promotion.Status == status.Value)
            };
        }

        if (scope.HasValue)
            query = query.Where(promotion => promotion.Scope == scope.Value);

        if (startDate.HasValue)
            query = query.Where(promotion => promotion.EndDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(promotion => promotion.StartDate <= endDate.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(promotion => promotion.CreatedAt)
            .ThenBy(promotion => promotion.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Promotion>(items, total);
    }

    public Task<Promotion?> GetDetailsAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default)
        => DetailsQuery().FirstOrDefaultAsync(
            promotion => promotion.Id == promotionId,
            cancellationToken);

    public Task<Promotion?> GetByCodeAsync(
        Guid boothId,
        string promotionCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = promotionCode.Trim().ToLower();
        return DetailsQuery().FirstOrDefaultAsync(
            promotion => promotion.BoothId == boothId
                && promotion.PromotionCode != null
                && promotion.PromotionCode.ToLower() == normalizedCode,
            cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        Guid boothId,
        string promotionCode,
        Guid? excludePromotionId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = promotionCode.Trim().ToLower();
        return ActiveQuery().AnyAsync(
            promotion => promotion.BoothId == boothId
                && promotion.PromotionCode != null
                && promotion.PromotionCode.ToLower() == normalizedCode
                && (!excludePromotionId.HasValue || promotion.Id != excludePromotionId.Value),
            cancellationToken);
    }

    public async Task<PagedResult<Promotion>> GetAvailablePagedAsync(
        IReadOnlyCollection<Guid> boothIds,
        DateTime now,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DetailsQuery()
            .AsNoTracking()
            .Where(promotion => boothIds.Contains(promotion.BoothId)
                && promotion.IsPublic
                && promotion.StartDate <= now
                && promotion.EndDate >= now
                && (promotion.Status == PromotionStatus.Active
                    || promotion.Status == PromotionStatus.Scheduled));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(promotion => promotion.EndDate)
            .ThenBy(promotion => promotion.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Promotion>(items, total);
    }

    public async Task<IReadOnlyCollection<Promotion>> GetAvailableAsync(
        IReadOnlyCollection<Guid> boothIds,
        DateTime now,
        CancellationToken cancellationToken = default)
        => await DetailsQuery()
            .AsNoTracking()
            .Where(promotion => boothIds.Contains(promotion.BoothId)
                && promotion.IsPublic
                && promotion.StartDate <= now
                && promotion.EndDate >= now
                && (promotion.Status == PromotionStatus.Active
                    || promotion.Status == PromotionStatus.Scheduled))
            .OrderBy(promotion => promotion.EndDate)
            .ThenBy(promotion => promotion.Title)
            .ToListAsync(cancellationToken);

    public async Task ReplaceScopeTargetsAsync(
        Promotion promotion,
        IReadOnlyCollection<Guid> foodItemIds,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken = default)
    {
        var requestedFoodItemIds = foodItemIds.ToHashSet();
        var requestedCategoryIds = categoryIds.ToHashSet();
        var removedFoodTargets = promotion.PromotionFoodItems
            .Where(target => !requestedFoodItemIds.Contains(target.FoodItemId))
            .ToList();
        var removedCategoryTargets = promotion.PromotionCategories
            .Where(target => !requestedCategoryIds.Contains(target.CategoryId))
            .ToList();

        _context.RemoveRange(removedFoodTargets);
        _context.RemoveRange(removedCategoryTargets);

        foreach (var target in removedFoodTargets)
            promotion.PromotionFoodItems.Remove(target);

        foreach (var target in removedCategoryTargets)
            promotion.PromotionCategories.Remove(target);

        var existingFoodItemIds = promotion.PromotionFoodItems
            .Select(target => target.FoodItemId)
            .ToHashSet();
        var existingCategoryIds = promotion.PromotionCategories
            .Select(target => target.CategoryId)
            .ToHashSet();
        var addedFoodTargets = requestedFoodItemIds
            .Where(foodItemId => !existingFoodItemIds.Contains(foodItemId))
            .Select(foodItemId => new PromotionFoodItem
            {
                PromotionId = promotion.Id,
                FoodItemId = foodItemId
            })
            .ToList();
        var addedCategoryTargets = requestedCategoryIds
            .Where(categoryId => !existingCategoryIds.Contains(categoryId))
            .Select(categoryId => new PromotionCategory
            {
                PromotionId = promotion.Id,
                CategoryId = categoryId
            })
            .ToList();

        foreach (var target in addedFoodTargets)
            promotion.PromotionFoodItems.Add(target);

        foreach (var target in addedCategoryTargets)
            promotion.PromotionCategories.Add(target);

        await _context.AddRangeAsync(addedFoodTargets, cancellationToken);
        await _context.AddRangeAsync(addedCategoryTargets, cancellationToken);
    }

    private IQueryable<Promotion> DetailsQuery()
        => ActiveQuery()
            .Include(promotion => promotion.Booth)
            .Include(promotion => promotion.PromotionFoodItems)
                .ThenInclude(target => target.FoodItem)
            .Include(promotion => promotion.PromotionCategories)
                .ThenInclude(target => target.Category)
            .Include(promotion => promotion.PromotionUsages)
            .AsSplitQuery();
}
