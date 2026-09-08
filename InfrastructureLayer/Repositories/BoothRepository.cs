using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace InfrastructureLayer.Repositories;

public class BoothRepository : GenericRepository<Booth>, IBoothRepository
{
    public BoothRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<CustomerBoothReadModel>> GetCustomerPagedAsync(
        Guid? marketId,
        string? search,
        bool? openNow,
        TimeOnly localTime,
        decimal? minimumRating,
        decimal? maximumRating,
        int page,
        int pageSize,
        string sort,
        CancellationToken cancellationToken = default)
    {
        var query = CustomerVisibleQuery();

        if (marketId.HasValue)
            query = query.Where(booth => booth.NightMarketId == marketId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLower();
            query = query.Where(booth => booth.BoothName.ToLower().Contains(keyword));
        }

        if (minimumRating.HasValue)
        {
            query = query.Where(booth => (booth.AverageRating ?? 0) >= minimumRating.Value);
        }

        if (maximumRating.HasValue)
        {
            query = query.Where(booth => (booth.AverageRating ?? 0) < maximumRating.Value);
        }

        if (openNow.HasValue)
        {
            var isOpen = IsCustomerOpenAt(localTime);
            query = query.Where(openNow.Value ? isOpen : Negate(isOpen));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        query = sort.ToLowerInvariant() switch
        {
            "name" => query.OrderBy(booth => booth.BoothName).ThenBy(booth => booth.Id),
            "rating" => query.OrderByDescending(booth => booth.AverageRating ?? 0).ThenBy(booth => booth.BoothName).ThenBy(booth => booth.Id),
            _ => query.OrderByDescending(booth => booth.IsFeatured).ThenBy(booth => booth.BoothName).ThenBy(booth => booth.Id)
        };

        var items = await ProjectCustomer(query, includeImages: false)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerBoothReadModel>(items, totalCount);
    }

    public Task<CustomerBoothReadModel?> GetCustomerByIdAsync(
        Guid boothId,
        CancellationToken cancellationToken = default)
        => ProjectCustomer(CustomerVisibleQuery().Where(booth => booth.Id == boothId), includeImages: true)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> CustomerVisibleExistsAsync(
        Guid boothId,
        CancellationToken cancellationToken = default)
        => CustomerVisibleQuery().AnyAsync(booth => booth.Id == boothId, cancellationToken);

    public async Task<PagedResult<NightMarketBoothCustomerReadModel>> GetCustomerByNightMarketPagedAsync(
        Guid nightMarketId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = CustomerVisibleQuery().Where(booth => booth.NightMarketId == nightMarketId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(booth => booth.IsFeatured)
            .ThenBy(booth => booth.BoothName)
            .ThenBy(booth => booth.Id)
            .Select(booth => new NightMarketBoothCustomerReadModel(
                booth.Id,
                booth.NightMarketId,
                booth.BoothName,
                booth.Description,
                booth.ThumbnailUrl,
                booth.SlotNumber,
                booth.Latitude,
                booth.Longitude,
                booth.OpenTime,
                booth.CloseTime,
                booth.NightMarket.OpeningHours,
                booth.NightMarket.ClosingHours,
                booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Open,
                booth.AverageRating,
                booth.IsFeatured))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NightMarketBoothCustomerReadModel>(items, totalCount);
    }

    public Task<Booth?> GetByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Include(booth => booth.NightMarket)
            .Include(booth => booth.Zone)
            .FirstOrDefaultAsync(
                booth => booth.BoothOwnerId == ownerId, cancellationToken);

    public Task<Booth?> GetByOwnerIdForUpdateAsync(
        Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet
            .FirstOrDefaultAsync(
                booth => booth.BoothOwnerId == ownerId, cancellationToken);

    public Task<Booth?> GetByOwnerIdWithAdminDetailsAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .AsSplitQuery()
            .Include(booth => booth.BoothOwner)
            .Include(booth => booth.NightMarket)
            .Include(booth => booth.Zone)
            .Include(booth => booth.BoothDocuments)
            .Include(booth => booth.BoothLocations.Where(location =>
                !location.IsDeleted && location.ReleasedAt == null))
                .ThenInclude(location => location.Layout)
            .Include(booth => booth.BoothLocations.Where(location =>
                !location.IsDeleted && location.ReleasedAt == null))
                .ThenInclude(location => location.Zone)
            .Include(booth => booth.BoothSubscriptions)
                .ThenInclude(subscription => subscription.Package)
            .FirstOrDefaultAsync(
                booth => booth.BoothOwnerId == ownerId,
                cancellationToken);

    public Task<bool> ExistsByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(
            booth => booth.BoothOwnerId == ownerId, cancellationToken);

    public async Task<Booth?> GetOwnedBoothAsync(Guid ownerId, Guid boothId)
        => await _dbSet.FirstOrDefaultAsync(booth => booth.Id == boothId && booth.BoothOwnerId == ownerId);

    private IQueryable<Booth> CustomerVisibleQuery()
        => _dbSet.AsNoTracking().Where(booth =>
            booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Active &&
            !booth.NightMarket.IsDeleted &&
            booth.NightMarket.ModerationStatus == DomainLayer.Enums.GeneralEnum.ModerationStatus.Active &&
            (booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Upcoming ||
             booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Open ||
             booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Closed));

    private static Expression<Func<Booth, bool>> IsCustomerOpenAt(TimeOnly localTime)
        => booth =>
            booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Open &&
            booth.NightMarket.OpeningHours.HasValue &&
            booth.NightMarket.ClosingHours.HasValue &&
            (booth.NightMarket.OpeningHours.Value == booth.NightMarket.ClosingHours.Value
                || (booth.NightMarket.OpeningHours.Value < booth.NightMarket.ClosingHours.Value
                    ? booth.NightMarket.OpeningHours.Value <= localTime && localTime < booth.NightMarket.ClosingHours.Value
                    : localTime >= booth.NightMarket.OpeningHours.Value || localTime < booth.NightMarket.ClosingHours.Value)) &&
            ((!booth.OpenTime.HasValue && !booth.CloseTime.HasValue) ||
             (booth.OpenTime.HasValue && booth.CloseTime.HasValue &&
              (booth.OpenTime.Value == booth.CloseTime.Value
                  || (booth.OpenTime.Value < booth.CloseTime.Value
                      ? booth.OpenTime.Value <= localTime && localTime < booth.CloseTime.Value
                      : localTime >= booth.OpenTime.Value || localTime < booth.CloseTime.Value))));

    private static Expression<Func<Booth, bool>> Negate(Expression<Func<Booth, bool>> predicate)
        => Expression.Lambda<Func<Booth, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

    private static IQueryable<CustomerBoothReadModel> ProjectCustomer(
        IQueryable<Booth> query,
        bool includeImages)
        => query.Select(booth => new CustomerBoothReadModel(
            booth.Id,
            booth.NightMarketId,
            booth.NightMarket.Name,
            booth.NightMarket.Address,
            booth.BoothName,
            booth.Description,
            booth.PhoneNumber,
            booth.ThumbnailUrl,
            booth.SlotNumber,
            booth.ZoneId,
            booth.Zone == null ? null : booth.Zone.ZoneName,
            booth.OpenTime,
            booth.CloseTime,
            booth.NightMarket.OpeningHours,
            booth.NightMarket.ClosingHours,
            booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Open,
            booth.AverageRating ?? 0,
            booth.Reviews.Count(review => review.IsVisible),
            booth.FoodItems.Count(item => item.IsAvailable && !item.IsDeleted && !item.Category.IsDeleted),
            booth.IsFeatured,
            includeImages
                ? booth.BoothImages.OrderBy(image => image.DisplayOrder).ThenBy(image => image.Id).Select(image => image.ImageUrl).ToList()
                : new List<string>(),
            booth.BoothLocations.Where(location => !location.IsDeleted && location.ReleasedAt == null
                && !location.Layout.IsDeleted && location.Layout.NightMarketId == booth.NightMarketId
                && location.Layout.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
                .OrderBy(location => location.Id).Select(location => (Guid?)location.LayoutId).FirstOrDefault(),
            booth.BoothLocations.Where(location => !location.IsDeleted && location.ReleasedAt == null
                && !location.Layout.IsDeleted && location.Layout.NightMarketId == booth.NightMarketId
                && location.Layout.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
                .OrderBy(location => location.Id).Select(location => (Guid?)location.LayoutNodeId).FirstOrDefault(),
            booth.BoothLocations.Where(location => !location.IsDeleted && location.ReleasedAt == null
                && !location.Layout.IsDeleted && location.Layout.NightMarketId == booth.NightMarketId
                && location.Layout.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
                .OrderBy(location => location.Id).Select(location => location.SlotNumber).FirstOrDefault(),
            booth.BoothLocations.Where(location => !location.IsDeleted && location.ReleasedAt == null
                && !location.Layout.IsDeleted && location.Layout.NightMarketId == booth.NightMarketId
                && location.Layout.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
                .OrderBy(location => location.Id).Select(location => location.ZoneId).FirstOrDefault(),
            booth.BoothLocations.Where(location => !location.IsDeleted && location.ReleasedAt == null
                && !location.Layout.IsDeleted && location.Layout.NightMarketId == booth.NightMarketId
                && location.Layout.Status == DomainLayer.Enums.GeneralEnum.MarketLayoutStatus.Active)
                .OrderBy(location => location.Id).Select(location => location.Zone == null ? null : location.Zone.ZoneName).FirstOrDefault()));
}
