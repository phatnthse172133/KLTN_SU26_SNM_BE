using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.InterfaceRepository;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;
using static DomainLayer.Enums.GeneralEnum;

namespace InfrastructureLayer.Repositories;

public class MarketLayoutRepository : GenericRepository<MarketLayout>, IMarketLayoutRepository
{
    public MarketLayoutRepository(SNMDbContext context) : base(context)
    {
    }

    protected override IQueryable<MarketLayout> ActiveQuery()
        => _dbSet.Where(l => !l.IsDeleted && (l.NightMarket == null || !l.NightMarket.IsDeleted));

    public async Task<PagedResult<MarketLayout>> GetActivePagedAsync(
        Guid nightMarketId, string? keyword, MarketLayoutStatus? status, int page, int pageSize,
        string sortBy, bool ascending, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(layout => layout.NightMarketId == nightMarketId
                && !layout.IsDeleted
                && !layout.NightMarket.IsDeleted
                && layout.NightMarket.ModerationStatus != ModerationStatus.Suspended);

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
            .AsSplitQuery()
            .Include(layout => layout.LayoutNodes.Where(node => !node.IsDeleted))
            .Include(layout => layout.BoothLocations.Where(location => !location.IsDeleted))
            .FirstOrDefaultAsync(
                layout => layout.Id == id && !layout.IsDeleted && !layout.NightMarket.IsDeleted,
                cancellationToken);

    public Task<MarketLayout?> GetActiveMapAsync(Guid nightMarketId, CancellationToken cancellationToken = default)
        => _dbSet.AsNoTracking()
            .FirstOrDefaultAsync(x => x.NightMarketId == nightMarketId && !x.IsDeleted &&
                x.Status == MarketLayoutStatus.Active && !x.NightMarket.IsDeleted, cancellationToken);

    public async Task<IReadOnlyCollection<LayoutNode>> GetNodesByLayoutIdAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
        => await _context.LayoutNodes.AsNoTracking()
            .Where(node => node.LayoutId == layoutId && !node.IsDeleted)
            .OrderBy(node => node.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<LayoutEdge>> GetEdgesByLayoutIdAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
        => await _context.LayoutEdges.AsNoTracking()
            .Where(edge =>
                edge.LayoutId == layoutId &&
                !edge.IsDeleted &&
                !edge.FromNode.IsDeleted &&
                !edge.ToNode.IsDeleted &&
                edge.FromNode.LayoutId == layoutId &&
                edge.ToNode.LayoutId == layoutId)
            .OrderBy(edge => edge.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<LayoutBlock>> GetBlocksByLayoutIdAsync(
        Guid layoutId, CancellationToken cancellationToken = default)
        => await _context.Set<LayoutBlock>().AsNoTracking()
            .Include(block => block.Zone)
            .Where(block => block.LayoutId == layoutId && !block.IsDeleted)
            .OrderBy(block => block.DisplayOrder)
            .ToListAsync(cancellationToken);

    public Task AcquireMarketLockAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
        => _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended(CAST({nightMarketId} AS text), 0))",
            cancellationToken);

    public async Task<int> GetNextVersionAsync(
        Guid nightMarketId, CancellationToken cancellationToken = default)
    {
        var currentMax = await _dbSet
            .Where(layout => layout.NightMarketId == nightMarketId && !layout.IsDeleted)
            .MaxAsync(layout => (int?)layout.Version, cancellationToken);
        return (currentMax ?? 0) + 1;
    }

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
                "Only an active layout can be cloned.", "LAYOUT_NOT_ACTIVE");

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
            Width = source.Width, Height = source.Height,
            MarketWidthMeters = source.MarketWidthMeters,
            MarketLengthMeters = source.MarketLengthMeters,
            PixelsPerMeter = source.PixelsPerMeter,
            CoordinateUnit = source.CoordinateUnit, MetersPerLayoutUnit = source.MetersPerLayoutUnit,
            DistanceCalibrationStatus = source.DistanceCalibrationStatus,
            GraphRevision = 1, Status = MarketLayoutStatus.Inactive, IsDeleted = false,
            CreatedAt = createdAt, UpdatedAt = createdAt
        };
        var sourceBlocks = await _context.Set<LayoutBlock>().AsNoTracking()
            .Where(block => block.LayoutId == source.Id && !block.IsDeleted)
            .ToListAsync(cancellationToken);
        var blockIds = sourceBlocks.ToDictionary(block => block.Id, _ => Guid.NewGuid());
        var blocks = sourceBlocks.Select(block => new LayoutBlock
        {
            Id = blockIds[block.Id], LayoutId = clone.Id, ZoneId = block.ZoneId,
            Type = block.Type, Name = block.Name, X = block.X, Y = block.Y,
            Width = block.Width, Height = block.Height, Rotation = block.Rotation,
            ConfigJson = block.ConfigJson, DisplayOrder = block.DisplayOrder,
            IsDeleted = false, CreatedAt = createdAt, UpdatedAt = createdAt
        }).ToList();
        var nodeIds = source.LayoutNodes.ToDictionary(node => node.Id, _ => Guid.NewGuid());
        var nodes = source.LayoutNodes.Select(node => new LayoutNode
        {
            Id = nodeIds[node.Id], LayoutId = clone.Id, ZoneId = node.ZoneId,
            LayoutBlockId = node.LayoutBlockId.HasValue && blockIds.TryGetValue(node.LayoutBlockId.Value, out var blockId)
                ? blockId : null,
            SlotCode = node.SlotCode,
            RowIndex = node.RowIndex, ColumnIndex = node.ColumnIndex,
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
        await _context.Set<LayoutBlock>().AddRangeAsync(blocks, cancellationToken);
        await _context.LayoutNodes.AddRangeAsync(nodes, cancellationToken);
        await _context.LayoutEdges.AddRangeAsync(edges, cancellationToken);
        await _context.BoothLocations.AddRangeAsync(locations, cancellationToken);
        await _context.LayoutNavigationAnchors.AddRangeAsync(anchors, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return clone;
    }

    public async Task SaveGraphTransactionalAsync(Guid layoutId, IEnumerable<LayoutBlock> blocks, IEnumerable<LayoutNode> nodes, IEnumerable<LayoutEdge> edges, IEnumerable<Zone>? zoneUpserts = null, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var blockList = blocks.ToList();
        var nodeList = nodes.ToList();
        var edgeList = edges.ToList();
        var now = DateTime.UtcNow;

        foreach (var zone in zoneUpserts ?? Enumerable.Empty<Zone>())
        {
            var existingZone = await _context.Zones
                .FirstOrDefaultAsync(z => z.Id == zone.Id && !z.IsDeleted, cancellationToken);
            if (existingZone == null)
            {
                await _context.Zones.AddAsync(zone, cancellationToken);
            }
            else
            {
                existingZone.ZoneName = zone.ZoneName;
                existingZone.Description = zone.Description;
                existingZone.Color = zone.Color;
                existingZone.Capacity = zone.Capacity;
                existingZone.UpdatedAt = now;
            }
        }

        var existingBlocks = await _context.Set<LayoutBlock>()
            .Where(block => block.LayoutId == layoutId)
            .ToListAsync(cancellationToken);
        var existingNodes = await _context.LayoutNodes
            .Where(node => node.LayoutId == layoutId)
            .ToListAsync(cancellationToken);
        var existingEdges = await _context.LayoutEdges
            .Where(edge => edge.LayoutId == layoutId)
            .ToListAsync(cancellationToken);

        var edgeIds = edgeList.Select(edge => edge.Id).ToHashSet();
        foreach (var edge in existingEdges.Where(edge => !edgeIds.Contains(edge.Id)))
        {
            edge.IsDeleted = true;
            edge.UpdatedAt = now;
        }

        var nodeIds = nodeList.Select(node => node.Id).ToHashSet();
        foreach (var node in existingNodes.Where(node => !nodeIds.Contains(node.Id)))
        {
            node.IsDeleted = true;
            node.UpdatedAt = now;
        }

        foreach (var node in nodeList)
        {
            var existingNode = existingNodes.FirstOrDefault(n => n.Id == node.Id);
            if (existingNode != null)
            {
                existingNode.ZoneId = node.ZoneId;
                existingNode.NodeName = node.NodeName;
                existingNode.NodeType = node.NodeType;
                existingNode.Xcoordinate = node.Xcoordinate;
                existingNode.Ycoordinate = node.Ycoordinate;
                existingNode.IsAccessible = node.IsAccessible;
                existingNode.IsStartingPoint = node.IsStartingPoint;
                // Persist full slot metadata — critical for generation and validation
                existingNode.SlotCode = node.SlotCode;
                existingNode.RowIndex = node.RowIndex;
                existingNode.ColumnIndex = node.ColumnIndex;
                existingNode.LayoutBlockId = node.LayoutBlockId;
                existingNode.IsDeleted = false;
                existingNode.UpdatedAt = now;
            }
            else
            {
                await _context.LayoutNodes.AddAsync(node, cancellationToken);
            }
        }

        foreach (var edge in edgeList)
        {
            var existingEdge = existingEdges.FirstOrDefault(e => e.Id == edge.Id);
            if (existingEdge != null)
            {
                existingEdge.FromNodeId = edge.FromNodeId;
                existingEdge.ToNodeId = edge.ToNodeId;
                existingEdge.Distance = edge.Distance;
                existingEdge.IsBidirectional = edge.IsBidirectional;
                existingEdge.IsAccessible = edge.IsAccessible;
                existingEdge.IsDeleted = false;
                existingEdge.UpdatedAt = now;
            }
            else
            {
                await _context.LayoutEdges.AddAsync(edge, cancellationToken);
            }
        }

        var blockIds = blockList.Select(b => b.Id).ToHashSet();
        foreach (var block in existingBlocks.Where(b => !blockIds.Contains(b.Id)))
        {
            block.IsDeleted = true;
            block.UpdatedAt = now;
        }

        foreach (var block in blockList)
        {
            var existingBlock = existingBlocks.FirstOrDefault(b => b.Id == block.Id);
            if (existingBlock != null)
            {
                existingBlock.ZoneId = block.ZoneId;
                existingBlock.Type = block.Type;
                existingBlock.Name = block.Name;
                existingBlock.X = block.X;
                existingBlock.Y = block.Y;
                existingBlock.Width = block.Width;
                existingBlock.Height = block.Height;
                existingBlock.Rotation = block.Rotation;
                existingBlock.DisplayOrder = block.DisplayOrder;
                existingBlock.ConfigJson = block.ConfigJson ?? existingBlock.ConfigJson;
                existingBlock.IsDeleted = false;
                existingBlock.UpdatedAt = now;
            }
            else
            {
                await _context.Set<LayoutBlock>().AddAsync(block, cancellationToken);
            }
        }

        var layout = await _context.MarketLayouts.FirstOrDefaultAsync(l => l.Id == layoutId, cancellationToken);
        if (layout != null)
        {
            layout.GraphRevision = checked(layout.GraphRevision + 1);
            layout.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
