using DomainLayer.Common;
using DomainLayer.Entities;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.InterfaceRepository;

public interface IFoodTagRepository : IGenericRepository<FoodTag>
{
    Task<PagedResult<FoodTag>> GetPagedTagsAsync(
        string? search,
        FoodTagGroup? tagGroup,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FoodTag>> GetActiveByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FoodTag>> GetActiveAsync(
        CancellationToken cancellationToken = default);

    Task<FoodTag?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);
}
