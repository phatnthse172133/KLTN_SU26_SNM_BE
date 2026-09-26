using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260926120000_BackfillLegacyActiveMarketMapPublishedAt")]
public sealed class BackfillLegacyActiveMarketMapPublishedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "MarketMaps" AS market_map
            SET "PublishedAt" = COALESCE(
                (
                    SELECT MAX(COALESCE(layout."UpdatedAt", layout."CreatedAt"))
                    FROM "MarketLayouts" AS layout
                    WHERE layout."MarketMapId" = market_map."Id"
                      AND layout."NightMarketId" = market_map."NightMarketId"
                      AND layout."Status" = 'Active'
                      AND layout."IsDeleted" = false
                ),
                market_map."UpdatedAt",
                market_map."CreatedAt",
                CURRENT_TIMESTAMP)
            WHERE market_map."Status" = 'Active'
              AND market_map."PublishedAt" IS NULL
              AND EXISTS (
                  SELECT 1
                  FROM "MarketLayouts" AS layout
                  WHERE layout."MarketMapId" = market_map."Id"
                    AND layout."NightMarketId" = market_map."NightMarketId"
                    AND layout."Status" = 'Active'
                    AND layout."IsDeleted" = false);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Publication timestamps are retained when rolling back this data repair.
    }
}
