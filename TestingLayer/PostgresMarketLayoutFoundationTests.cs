using InfrastructureLayer.Data;
using InfrastructureLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TestingLayer;

public class PostgresMarketLayoutFoundationTests
{
    [Fact]
    [Trait("Category", "PostgreSQLIntegration")]
    public async Task LatestMigrations_EnforceFoundationGraphConstraintsAndPerLayoutLocationIndexes()
    {
        var adminConnection = Environment.GetEnvironmentVariable("SNM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;

        var databaseName = $"snm_layout_foundation_{Guid.NewGuid():N}";
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin).ExecuteNonQueryAsync();
        var testBuilder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName };
        try
        {
            var options = new DbContextOptionsBuilder<SNMDbContext>().UseNpgsql(testBuilder.ConnectionString).Options;
            await using (var db = new SNMDbContext(options)) await db.Database.MigrateAsync();
            await using var connection = new NpgsqlConnection(testBuilder.ConnectionString);
            await connection.OpenAsync();

            Assert.Equal(1L, await ScalarAsync(connection,
                "SELECT count(*) FROM pg_constraint WHERE conname = 'ck_layoutedge_distance_positive'"));
            Assert.Equal(1L, await ScalarAsync(connection,
                "SELECT count(*) FROM pg_constraint WHERE conname = 'ck_marketlayout_positive_scale'"));
            Assert.Equal(1L, await ScalarAsync(connection,
                "SELECT count(*) FROM pg_constraint WHERE conname = 'ck_marketlayout_graph_revision_positive'"));
            Assert.Equal(2L, await ScalarAsync(connection,
                "SELECT count(*) FROM pg_indexes WHERE tablename = 'BoothLocations' AND indexname IN ('ux_boothlocation_active_layout_booth', 'ux_boothlocation_active_layout_node') AND indexdef LIKE '%\"LayoutId\"%'"));
            Assert.Equal(3L, await ScalarAsync(connection,
                "SELECT count(*) FROM pg_constraint WHERE conname IN ('ck_navigationanchor_hours_pair', 'ck_navigationanchor_latitude', 'ck_navigationanchor_longitude')"));
            Assert.Equal(3L, await ScalarAsync(connection,
                "SELECT count(*) FROM pg_constraint WHERE conname IN ('ck_navigationanchor_qr_hash', 'ck_navigationanchor_qr_validity', 'ck_navigationanchor_token_version')"));
            Assert.Equal(1L, await ScalarAsync(connection,
                "SELECT count(*) FROM pg_indexes WHERE tablename = 'LayoutNavigationAnchors' AND indexname = 'ux_navigationanchor_layout_code'"));

            var marketId = Guid.NewGuid(); var layoutId = Guid.NewGuid(); var fromId = Guid.NewGuid(); var toId = Guid.NewGuid();
            await ExecuteAsync(connection, """
                INSERT INTO "NightMarket" ("Id", "Name", "Address", "Status", "ModerationStatus", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES (@market, 'Foundation', 'Test', 'Active', 'Active', false, now(), now());
                INSERT INTO "MarketLayouts" ("Id", "NightMarketId", "LayoutName", "Version", "Width", "Height", "Status", "GraphRevision", "CreatedAt", "UpdatedAt")
                VALUES (@layout, @market, 'Draft', 1, 100, 100, 'Draft', 1, now(), now());
                INSERT INTO "LayoutNodes" ("Id", "LayoutId", "NodeName", "NodeType", "XCoordinate", "YCoordinate", "IsAccessible", "IsStartingPoint", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES (@from, @layout, 'From', 'Junction', 0, 0, true, false, false, now(), now()),
                       (@to, @layout, 'To', 'Junction', 10, 0, true, false, false, now(), now());
                """, ("market", marketId), ("layout", layoutId), ("from", fromId), ("to", toId));

            await AssertCheckViolationAsync(connection,
                "UPDATE \"MarketLayouts\" SET \"GraphRevision\" = 0 WHERE \"Id\" = @id", ("id", layoutId));
            await AssertCheckViolationAsync(connection,
                "UPDATE \"MarketLayouts\" SET \"MetersPerLayoutUnit\" = 0 WHERE \"Id\" = @id", ("id", layoutId));
            await AssertCheckViolationAsync(connection, """
                INSERT INTO "LayoutEdges" ("Id", "LayoutId", "FromNodeId", "ToNodeId", "Distance", "IsBidirectional", "IsAccessible", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES (@id, @layout, @from, @to, 0, true, true, false, now(), now())
                """, ("id", Guid.NewGuid()), ("layout", layoutId), ("from", fromId), ("to", toId));

            await AssertSqlStateAsync(connection, PostgresErrorCodes.ForeignKeyViolation, """
                INSERT INTO "LayoutEdges" ("Id", "LayoutId", "FromNodeId", "ToNodeId", "Distance", "IsBidirectional", "IsAccessible", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES (@id, @layout, @missing, @to, 1, true, true, false, now(), now())
                """, ("id", Guid.NewGuid()), ("layout", layoutId), ("missing", Guid.NewGuid()), ("to", toId));

            var anchorId = Guid.NewGuid();
            await ExecuteAsync(connection, """
                INSERT INTO "LayoutNavigationAnchors" ("Id", "LayoutId", "LayoutNodeId", "AnchorType", "AnchorCode", "DisplayName", "Latitude", "Longitude", "IsCustomerAccessible", "IsActive", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES (@id, @layout, @node, 'Entrance', 'MAIN_ENTRANCE', 'Main entrance', 10.8751000, 106.8002000, true, true, false, now(), now())
                """, ("id", anchorId), ("layout", layoutId), ("node", fromId));
            await AssertCheckViolationAsync(connection,
                "UPDATE \"LayoutNavigationAnchors\" SET \"Latitude\" = 91 WHERE \"Id\" = @id", ("id", anchorId));
            await AssertSqlStateAsync(connection, PostgresErrorCodes.UniqueViolation, """
                INSERT INTO "LayoutNavigationAnchors" ("Id", "LayoutId", "LayoutNodeId", "AnchorType", "AnchorCode", "DisplayName", "Latitude", "Longitude", "IsCustomerAccessible", "IsActive", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES (@id, @layout, @node, 'Entrance', 'MAIN_ENTRANCE', 'Duplicate', 10, 106, true, true, false, now(), now())
                """, ("id", Guid.NewGuid()), ("layout", layoutId), ("node", fromId));
            await AssertSqlStateAsync(connection, PostgresErrorCodes.RestrictViolation,
                "DELETE FROM \"LayoutNodes\" WHERE \"Id\" = @id", ("id", fromId));

            await ExecuteAsync(connection, "UPDATE \"MarketLayouts\" SET \"Status\" = 'Active' WHERE \"Id\" = @id", ("id", layoutId));
            await AssertSqlStateAsync(connection, PostgresErrorCodes.UniqueViolation, """
                INSERT INTO "MarketLayouts" ("Id", "NightMarketId", "LayoutName", "Version", "Width", "Height", "Status", "GraphRevision", "CreatedAt", "UpdatedAt")
                VALUES (@id, @market, 'Second Active', 2, 100, 100, 'Active', 1, now(), now())
                """, ("id", Guid.NewGuid()), ("market", marketId));

            var roleId = Guid.NewGuid(); var ownerId = Guid.NewGuid(); var registrationId = Guid.NewGuid(); var boothId = Guid.NewGuid();
            await ExecuteAsync(connection, """
                INSERT INTO "Role" ("Id", "RoleName", "CreatedAt", "UpdatedAt")
                VALUES (@role, 'FoundationBoothOwner', now(), now());
                INSERT INTO "User" ("Id", "RoleId", "UserName", "PasswordHash", "FullName", "Email", "Status", "CreatedAt", "UpdatedAt")
                VALUES (@owner, @role, 'foundation-owner', 'not-used', 'Foundation Owner', 'foundation-owner@example.test', 'Active', now(), now());
                INSERT INTO "BoothRegistrations" ("Id", "OwnerId", "RequestedNightMarketId", "BoothName", "Status", "CreatedAt", "UpdatedAt")
                VALUES (@registration, @owner, @market, 'Foundation Booth', 2, now(), now());
                INSERT INTO "Booth" ("Id", "RegistrationId", "NightMarketId", "BoothOwnerId", "BoothName", "Status", "CreatedAt", "UpdatedAt")
                VALUES (@booth, @registration, @market, @owner, 'Foundation Booth', 'Active', now(), now());
                INSERT INTO "LayoutEdges" ("Id", "LayoutId", "FromNodeId", "ToNodeId", "Distance", "IsBidirectional", "IsAccessible", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES (@edge, @layout, @from, @to, 10, true, true, false, now(), now());
                INSERT INTO "BoothLocations" ("Id", "BoothId", "LayoutId", "LayoutNodeId", "XCoordinate", "YCoordinate", "IsDeleted", "CreatedAt", "UpdatedAt")
                VALUES (@location, @booth, @layout, @to, 10, 0, false, now(), now());
                """, ("role", roleId), ("owner", ownerId), ("registration", registrationId),
                ("market", marketId), ("booth", boothId), ("edge", Guid.NewGuid()), ("layout", layoutId),
                ("from", fromId), ("to", toId), ("location", Guid.NewGuid()));

            await using var cloneDb = new SNMDbContext(options);
            var sourceNodes = await cloneDb.LayoutNodes.CountAsync(x => x.LayoutId == layoutId && !x.IsDeleted);
            var sourceEdges = await cloneDb.LayoutEdges.CountAsync(x => x.LayoutId == layoutId && !x.IsDeleted);
            var sourceLocations = await cloneDb.BoothLocations.CountAsync(x => x.LayoutId == layoutId && !x.IsDeleted);
            var sourceAnchors = await cloneDb.LayoutNavigationAnchors.CountAsync(x => x.LayoutId == layoutId && !x.IsDeleted);
            var clone = await new MarketLayoutRepository(cloneDb).CloneToDraftAsync(layoutId, "PostgreSQL clone", DateTime.UtcNow);
            Assert.Equal(sourceNodes, await cloneDb.LayoutNodes.CountAsync(x => x.LayoutId == clone.Id && !x.IsDeleted));
            Assert.Equal(sourceEdges, await cloneDb.LayoutEdges.CountAsync(x => x.LayoutId == clone.Id && !x.IsDeleted));
            Assert.Equal(sourceLocations, await cloneDb.BoothLocations.CountAsync(x => x.LayoutId == clone.Id && !x.IsDeleted));
            Assert.Equal(sourceAnchors, await cloneDb.LayoutNavigationAnchors.CountAsync(x => x.LayoutId == clone.Id && !x.IsDeleted));
            var cloneNodeIds = (await cloneDb.LayoutNodes.Where(x => x.LayoutId == clone.Id).Select(x => x.Id).ToListAsync()).ToHashSet();
            Assert.All(await cloneDb.LayoutEdges.Where(x => x.LayoutId == clone.Id).ToListAsync(), edge =>
                Assert.True(cloneNodeIds.Contains(edge.FromNodeId) && cloneNodeIds.Contains(edge.ToNodeId)));
            Assert.All(await cloneDb.LayoutNavigationAnchors.Where(x => x.LayoutId == clone.Id).ToListAsync(), anchor =>
                Assert.Contains(anchor.LayoutNodeId, cloneNodeIds));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var cleanup = new NpgsqlConnection(adminBuilder.ConnectionString);
            await cleanup.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", cleanup).ExecuteNonQueryAsync();
        }
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql)
        => Convert.ToInt64(await new NpgsqlCommand(sql, connection).ExecuteScalarAsync());

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertCheckViolationAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await AssertSqlStateAsync(connection, PostgresErrorCodes.CheckViolation, sql, parameters);
    }

    private static async Task AssertSqlStateAsync(NpgsqlConnection connection, string sqlState, string sql, params (string Name, object Value)[] parameters)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, sql, parameters));
        Assert.Equal(sqlState, error.SqlState);
    }

}
