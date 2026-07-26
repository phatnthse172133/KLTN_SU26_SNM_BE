using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Repositories;

public class BoothRepository : GenericRepository<Booth>, IBoothRepository
{
    public BoothRepository(SNMDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<NightMarketBoothCustomerReadModel>> GetCustomerByNightMarketPagedAsync(
        Guid nightMarketId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking().Where(booth =>
            booth.NightMarketId == nightMarketId &&
            booth.Status == DomainLayer.Enums.GeneralEnum.BoothStatus.Active &&
            !booth.NightMarket.IsDeleted &&
            booth.NightMarket.ModerationStatus == DomainLayer.Enums.GeneralEnum.ModerationStatus.Active &&
            (booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Upcoming ||
             booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Open ||
             booth.NightMarket.Status == DomainLayer.Enums.GeneralEnum.NightMarketStatus.Closed));

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
                booth.AverageRating,
                booth.IsFeatured))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NightMarketBoothCustomerReadModel>(items, totalCount);
    }

    public Task<Booth?> GetByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking().FirstOrDefaultAsync(
            booth => booth.BoothOwnerId == ownerId, cancellationToken);

    public Task<bool> ExistsByOwnerIdAsync(
        Guid ownerId, CancellationToken cancellationToken = default)
        => _dbSet.AnyAsync(
            booth => booth.BoothOwnerId == ownerId, cancellationToken);

    public async Task<Booth?> GetOwnedBoothAsync(Guid ownerId, Guid boothId)
        => await _dbSet.FirstOrDefaultAsync(booth => booth.Id == boothId && booth.BoothOwnerId == ownerId);
}
