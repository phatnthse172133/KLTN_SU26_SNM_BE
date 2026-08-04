using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class MarketLayoutRepository : GenericRepository<MarketLayout>, IMarketLayoutRepository
{
    public MarketLayoutRepository(SNMDbContext context) : base(context) { }

    public async Task<PagedResult<MarketLayout>> GetActivePagedAsync(
        Guid nightMarketId, string? keyword, MarketLayoutStatus? status, int page, int pageSize,
        string sortBy, bool ascending, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(layout => layout.NightMarketId == nightMarketId && !layout.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim().ToLower();
            query = query.Where(layout => layout.LayoutName.ToLower().Contains(normalized));
        }

        if (status.HasValue)
            query = query.Where(layout => layout.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        query = (sortBy.ToLowerInvariant(), ascending) switch
        {
            ("name", true) => query.OrderBy(layout => layout.LayoutName),
            ("name", false) => query.OrderByDescending(layout => layout.LayoutName),
            ("version", true) => query.OrderBy(layout => layout.Version),
            ("version", false) => query.OrderByDescending(layout => layout.Version),
            ("status", true) => query.OrderBy(layout => layout.Status),
            ("status", false) => query.OrderByDescending(layout => layout.Status),
            ("updatedat", true) => query.OrderBy(layout => layout.UpdatedAt),
            ("updatedat", false) => query.OrderByDescending(layout => layout.UpdatedAt),
            ("createdat", true) => query.OrderBy(layout => layout.CreatedAt),
            _ => query.OrderByDescending(layout => layout.CreatedAt)
        };

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<MarketLayout>(items, total);
    }

    public Task<MarketLayout?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.FirstOrDefaultAsync(
            layout => layout.Id == id && !layout.IsDeleted && !layout.NightMarket.IsDeleted,
            cancellationToken);

    public Task<MarketLayout?> GetEditorLayoutAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .Include(layout => layout.LayoutNodes.Where(node => !node.IsDeleted))
            .Include(layout => layout.BoothLocations.Where(location => !location.IsDeleted))
            .Include(layout => layout.NavigationAnchors.Where(anchor => !anchor.IsDeleted))
            .FirstOrDefaultAsync(
                layout => layout.Id == id && !layout.IsDeleted && !layout.NightMarket.IsDeleted,
                cancellationToken);

    public Task<MarketLayout?> GetActiveMapAsync(Guid nightMarketId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .FirstOrDefaultAsync(x => x.NightMarketId == nightMarketId && !x.IsDeleted &&
                x.Status == MarketLayoutStatus.Active && !x.NightMarket.IsDeleted, cancellationToken);

    public async Task<IReadOnlyCollection<LayoutEdge>> GetEdgesByLayoutIdAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
        => await _context.LayoutEdges.AsNoTracking()
            .Where(edge => edge.LayoutId == layoutId && !edge.IsDeleted)
            .OrderBy(edge => edge.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> ActiveNameOrVersionExistsAsync(
        Guid nightMarketId, string name, int version, Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLower();
        return _dbSet.AnyAsync(layout =>
            layout.NightMarketId == nightMarketId &&
            !layout.IsDeleted &&
            (layout.LayoutName.ToLower() == normalized || layout.Version == version) &&
            (!excludeId.HasValue || layout.Id != excludeId.Value), cancellationToken);
    }

    public async Task ActivateExclusiveAsync(
        Guid nightMarketId, Guid activeLayoutId, DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended(CAST({nightMarketId} AS text), 0))",
            cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "MarketLayouts"
            SET "Status" = 'Inactive',
                "UpdatedAt" = {updatedAt}
            WHERE "NightMarketId" = {nightMarketId}
              AND "IsDeleted" = false
              AND "Id" <> {activeLayoutId}
              AND "Status" = 'Active'
            """, cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "MarketLayouts"
            SET "Status" = 'Active',
                "UpdatedAt" = {updatedAt}
            WHERE "Id" = {activeLayoutId}
              AND "NightMarketId" = {nightMarketId}
              AND "IsDeleted" = false
            """, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<MarketLayout> CloneToDraftAsync(
        Guid sourceLayoutId, string? layoutName, DateTime createdAt,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var source = await _dbSet.AsNoTracking()
            .Include(layout => layout.LayoutNodes.Where(node => !node.IsDeleted))
            .Include(layout => layout.LayoutEdges.Where(edge => !edge.IsDeleted))
            .Include(layout => layout.BoothLocations.Where(location => !location.IsDeleted))
            .Include(layout => layout.NavigationAnchors.Where(anchor => !anchor.IsDeleted))
            .FirstOrDefaultAsync(layout => layout.Id == sourceLayoutId && !layout.IsDeleted, cancellationToken)
            ?? throw ApplicationLayer.Exceptions.AppException.NotFound("Market layout was not found.");

        if (source.Status != MarketLayoutStatus.Active)
            throw ApplicationLayer.Exceptions.AppException.Conflict(
                "Only an active layout can be cloned to a draft.", "LAYOUT_NOT_ACTIVE");

        if (_context.Database.IsRelational())
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended(CAST({source.NightMarketId} AS text), 1))",
                cancellationToken);

        var nextVersion = await _dbSet
            .Where(layout => layout.NightMarketId == source.NightMarketId && !layout.IsDeleted)
            .MaxAsync(layout => (int?)layout.Version, cancellationToken) + 1 ?? 1;
        var nextName = string.IsNullOrWhiteSpace(layoutName)
            ? $"{source.LayoutName} v{nextVersion}"
            : layoutName.Trim();

        if (await _dbSet.AnyAsync(layout => layout.NightMarketId == source.NightMarketId &&
                !layout.IsDeleted && layout.LayoutName.ToLower() == nextName.ToLower(), cancellationToken))
            throw ApplicationLayer.Exceptions.AppException.Conflict(
                "Layout name already exists in this night market.", "LAYOUT_NAME_CONFLICT");

        var clone = new MarketLayout
        {
            Id = Guid.NewGuid(), NightMarketId = source.NightMarketId, LayoutName = nextName,
            Version = nextVersion, LayoutImageUrl = source.LayoutImageUrl,
            Width = source.Width, Height = source.Height, CoordinateUnit = source.CoordinateUnit,
            MetersPerLayoutUnit = source.MetersPerLayoutUnit,
            DistanceCalibrationStatus = source.DistanceCalibrationStatus,
            GraphRevision = 1, Status = MarketLayoutStatus.Draft, IsDeleted = false,
            CreatedAt = createdAt, UpdatedAt = createdAt
        };
        var nodeIds = source.LayoutNodes.ToDictionary(node => node.Id, _ => Guid.NewGuid());
        var nodes = source.LayoutNodes.Select(node => new LayoutNode
        {
            Id = nodeIds[node.Id], LayoutId = clone.Id, ZoneId = node.ZoneId,
            NodeName = node.NodeName, NodeType = node.NodeType,
            Xcoordinate = node.Xcoordinate, Ycoordinate = node.Ycoordinate,
            IsAccessible = node.IsAccessible, IsStartingPoint = node.IsStartingPoint,
            IsDeleted = false, CreatedAt = createdAt, UpdatedAt = createdAt
        }).ToList();
        var edges = source.LayoutEdges.Select(edge => new LayoutEdge
        {
            Id = Guid.NewGuid(), LayoutId = clone.Id,
            FromNodeId = nodeIds[edge.FromNodeId], ToNodeId = nodeIds[edge.ToNodeId],
            Distance = edge.Distance, IsBidirectional = edge.IsBidirectional,
            IsAccessible = edge.IsAccessible, IsDeleted = false,
            CreatedAt = createdAt, UpdatedAt = createdAt
        }).ToList();
        var locations = source.BoothLocations.Select(location => new BoothLocation
        {
            Id = Guid.NewGuid(), BoothId = location.BoothId, LayoutId = clone.Id,
            LayoutNodeId = nodeIds[location.LayoutNodeId], ZoneId = location.ZoneId,
            SlotNumber = location.SlotNumber, Xcoordinate = location.Xcoordinate,
            Ycoordinate = location.Ycoordinate, IsDeleted = false,
            CreatedAt = createdAt, UpdatedAt = createdAt
        }).ToList();
        var anchors = source.NavigationAnchors.Select(anchor => new LayoutNavigationAnchor
        {
            Id = Guid.NewGuid(), LayoutId = clone.Id, LayoutNodeId = nodeIds[anchor.LayoutNodeId],
            AnchorType = anchor.AnchorType, AnchorCode = anchor.AnchorCode, DisplayName = anchor.DisplayName,
            Latitude = anchor.Latitude, Longitude = anchor.Longitude,
            IsCustomerAccessible = anchor.IsCustomerAccessible, IsActive = anchor.IsActive,
            OpeningTime = anchor.OpeningTime, ClosingTime = anchor.ClosingTime,
            PublicTokenHash = null, TokenVersion = 1, IsQrEnabled = false,
            QrValidFrom = null, QrValidUntil = null,
            IsDeleted = false, CreatedAt = createdAt, UpdatedAt = createdAt
        }).ToList();

        await _dbSet.AddAsync(clone, cancellationToken);
        await _context.LayoutNodes.AddRangeAsync(nodes, cancellationToken);
        await _context.LayoutEdges.AddRangeAsync(edges, cancellationToken);
        await _context.BoothLocations.AddRangeAsync(locations, cancellationToken);
        await _context.LayoutNavigationAnchors.AddRangeAsync(anchors, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return clone;
    }
}
