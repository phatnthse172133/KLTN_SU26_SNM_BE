using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class ZoneRepository : GenericRepository<Zone>, IZoneRepository
{
    public ZoneRepository(SNMDbContext context) : base(context) { }

    public async Task<PagedResult<Zone>> GetActivePagedAsync(
        Guid nightMarketId, string? keyword, ZoneStatus? status, int page, int pageSize,
        string sortBy, bool ascending, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(zone => zone.NightMarketId == nightMarketId && !zone.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim().ToLower();
            query = query.Where(zone =>
                zone.ZoneName.ToLower().Contains(normalized) ||
                (zone.Description != null && zone.Description.ToLower().Contains(normalized)));
        }

        if (status.HasValue)
            query = query.Where(zone => zone.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        query = (sortBy.ToLowerInvariant(), ascending) switch
        {
            ("name", true) => query.OrderBy(zone => zone.ZoneName),
            ("name", false) => query.OrderByDescending(zone => zone.ZoneName),
            ("status", true) => query.OrderBy(zone => zone.Status),
            ("status", false) => query.OrderByDescending(zone => zone.Status),
            ("updatedat", true) => query.OrderBy(zone => zone.UpdatedAt),
            ("updatedat", false) => query.OrderByDescending(zone => zone.UpdatedAt),
            ("createdat", true) => query.OrderBy(zone => zone.CreatedAt),
            _ => query.OrderByDescending(zone => zone.CreatedAt)
        };

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Zone>(items, total);
    }

    public Task<Zone?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(
            zone => zone.Id == id && !zone.IsDeleted && !zone.NightMarket.IsDeleted,
            cancellationToken);

    public async Task<IReadOnlyCollection<Zone>> GetActiveByNightMarketIdAsync(
        Guid nightMarketId, bool activeStatusOnly = false, CancellationToken cancellationToken = default)
        => await _dbSet.AsNoTracking()
            .Where(zone => zone.NightMarketId == nightMarketId && !zone.IsDeleted
                && (!activeStatusOnly || zone.Status == ZoneStatus.Active))
            .OrderBy(zone => zone.ZoneName)
            .ToListAsync(cancellationToken);

    public Task<bool> ActiveNameExistsAsync(
        Guid nightMarketId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        return _dbSet.AnyAsync(zone =>
            zone.NightMarketId == nightMarketId &&
            !zone.IsDeleted &&
            zone.ZoneName.ToLower() == normalized &&
            (!excludeId.HasValue || zone.Id != excludeId.Value), cancellationToken);
    }

    public Task<bool> ActiveZoneCodeExistsAsync(
        Guid nightMarketId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _dbSet.AnyAsync(zone =>
            zone.NightMarketId == nightMarketId &&
            !zone.IsDeleted &&
            zone.ZoneCode != null &&
            zone.ZoneCode.ToUpper() == normalized &&
            (!excludeId.HasValue || zone.Id != excludeId.Value), cancellationToken);
    }

    public async Task<Dictionary<Guid, int>> GetAssignedSlotCountsByMarketAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
    {
        // Count BoothSlot nodes that have an active BoothLocation, grouped by ZoneId
        return await _context.Set<LayoutNode>()
            .AsNoTracking()
            .Where(n =>
                n.Layout.NightMarketId == nightMarketId &&
                !n.IsDeleted &&
                n.ZoneId.HasValue &&
                n.BoothLocations.Any(bl => !bl.IsDeleted && bl.ReleasedAt == null))
            .GroupBy(n => n.ZoneId!.Value)
            .Select(g => new { ZoneId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ZoneId, x => x.Count, cancellationToken);
    }
}
