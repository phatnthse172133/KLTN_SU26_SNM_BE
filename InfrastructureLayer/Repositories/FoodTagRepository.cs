using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class FoodTagRepository : GenericRepository<FoodTag>, IFoodTagRepository
{
    public FoodTagRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<FoodTag>> GetPagedTagsAsync(
        string? search,
        FoodTagGroup? tagGroup,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = ActiveQuery();
        query = query.Where(tag => tag.IsSystem && tag.Status == FoodTagStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(tag =>
                tag.Name.ToLower().Contains(keyword)
                || tag.Code.ToLower().Contains(keyword)
                || (tag.Description != null && tag.Description.ToLower().Contains(keyword)));
        }

        if (tagGroup.HasValue)
        {
            query = query.Where(tag => tag.TagGroup == tagGroup.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(tag => tag.TagGroup)
            .ThenBy(tag => tag.DisplayOrder)
            .ThenBy(tag => tag.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FoodTag>(items, total);
    }

    public async Task<IReadOnlyCollection<FoodTag>> GetActiveByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
        => await ActiveQuery()
            .Where(tag => ids.Contains(tag.Id) && tag.Status == FoodTagStatus.Active)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<FoodTag>> GetActiveAsync(
        CancellationToken cancellationToken = default)
        => await ActiveQuery()
            .Where(tag => tag.Status == FoodTagStatus.Active)
            .OrderBy(tag => tag.TagGroup)
            .ThenBy(tag => tag.DisplayOrder)
            .ThenBy(tag => tag.Name)
            .ToListAsync(cancellationToken);

    public Task<FoodTag?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => ActiveQuery().FirstOrDefaultAsync(tag => tag.Code == code, cancellationToken);
}
