using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketMapFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    NightMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Draft'::character varying"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("MarketMaps_pkey", x => x.Id);
                    table.UniqueConstraint("AK_MarketMaps_Id_NightMarketId", x => new { x.Id, x.NightMarketId });
                    table.ForeignKey(
                        name: "MarketMaps_NightMarketId_fkey",
                        column: x => x.NightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Add the ownership column as nullable first so existing rows remain
            // valid while their evidence-based aggregate is created below.
            migrationBuilder.AddColumn<Guid>(
                name: "MarketMapId",
                table: "MarketLayouts",
                type: "uuid",
                nullable: true);

            // The only historical composition supported by existing data is the
            // set of currently active, non-deleted sections. Group exactly those
            // sections into one Active map per market. PublishedAt remains null
            // because no reliable overall-map publication timestamp exists.
            migrationBuilder.Sql("""
                INSERT INTO "MarketMaps"
                    ("Id", "NightMarketId", "Name", "Version", "Status", "CreatedAt", "PublishedAt", "UpdatedAt")
                SELECT uuid_generate_v4(), active_layouts."NightMarketId",
                       'Current Market Map', 1, 'Active', CURRENT_TIMESTAMP, NULL, CURRENT_TIMESTAMP
                FROM (
                    SELECT DISTINCT "NightMarketId"
                    FROM "MarketLayouts"
                    WHERE "Status" = 'Active' AND "IsDeleted" = false
                ) AS active_layouts;

                UPDATE "MarketLayouts" AS layout
                SET "MarketMapId" = map."Id"
                FROM "MarketMaps" AS map
                WHERE layout."NightMarketId" = map."NightMarketId"
                  AND layout."Status" = 'Active'
                  AND layout."IsDeleted" = false
                  AND map."Status" = 'Active'
                  AND map."Version" = 1;
                """);

            // No legacy data proves that Draft/Inactive/Archived/deleted layouts
            // shared an overall release. Preserve every such layout as its own
            // deterministic single-layout map instead of manufacturing history.
            migrationBuilder.Sql("""
                WITH legacy_layouts AS (
                    SELECT layout."Id" AS "LayoutId",
                           layout."NightMarketId",
                           CASE
                               WHEN layout."Status" = 'Draft' AND layout."IsDeleted" = false
                                   THEN 'Legacy Draft Map ' || layout."Id"::text
                               ELSE 'Legacy Layout Snapshot ' || layout."Id"::text
                           END AS "MapName",
                           CASE
                               WHEN layout."Status" = 'Draft' AND layout."IsDeleted" = false
                                   THEN 'Draft'
                               ELSE 'Archived'
                           END AS "MapStatus",
                           (
                               ROW_NUMBER() OVER (
                                   PARTITION BY layout."NightMarketId"
                                   ORDER BY layout."CreatedAt", layout."Id"
                               )
                               + CASE WHEN EXISTS (
                                   SELECT 1
                                   FROM "MarketMaps" AS active_map
                                   WHERE active_map."NightMarketId" = layout."NightMarketId"
                                     AND active_map."Status" = 'Active'
                               ) THEN 1 ELSE 0 END
                           )::integer AS "MapVersion"
                    FROM "MarketLayouts" AS layout
                    WHERE layout."MarketMapId" IS NULL
                )
                INSERT INTO "MarketMaps"
                    ("Id", "NightMarketId", "Name", "Version", "Status", "CreatedAt", "PublishedAt", "UpdatedAt")
                SELECT uuid_generate_v4(), legacy."NightMarketId", legacy."MapName",
                       legacy."MapVersion", legacy."MapStatus", CURRENT_TIMESTAMP, NULL, CURRENT_TIMESTAMP
                FROM legacy_layouts AS legacy;

                UPDATE "MarketLayouts" AS layout
                SET "MarketMapId" = map."Id"
                FROM "MarketMaps" AS map
                WHERE layout."MarketMapId" IS NULL
                  AND layout."NightMarketId" = map."NightMarketId"
                  AND map."Name" = CASE
                      WHEN layout."Status" = 'Draft' AND layout."IsDeleted" = false
                          THEN 'Legacy Draft Map ' || layout."Id"::text
                      ELSE 'Legacy Layout Snapshot ' || layout."Id"::text
                  END;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "MarketLayouts" WHERE "MarketMapId" IS NULL) THEN
                        RAISE EXCEPTION 'MarketMap backfill failed: one or more MarketLayouts remain unlinked';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "MarketMapId",
                table: "MarketLayouts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketLayouts_MarketMapId_NightMarketId",
                table: "MarketLayouts",
                columns: new[] { "MarketMapId", "NightMarketId" });

            migrationBuilder.CreateIndex(
                name: "ux_marketmap_market_version",
                table: "MarketMaps",
                columns: new[] { "NightMarketId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_marketmap_one_active_per_market",
                table: "MarketMaps",
                column: "NightMarketId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.AddForeignKey(
                name: "MarketLayouts_MarketMapId_NightMarketId_fkey",
                table: "MarketLayouts",
                columns: new[] { "MarketMapId", "NightMarketId" },
                principalTable: "MarketMaps",
                principalColumns: new[] { "Id", "NightMarketId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "MarketLayouts_MarketMapId_NightMarketId_fkey",
                table: "MarketLayouts");

            migrationBuilder.DropTable(
                name: "MarketMaps");

            migrationBuilder.DropIndex(
                name: "IX_MarketLayouts_MarketMapId_NightMarketId",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "MarketMapId",
                table: "MarketLayouts");
        }
    }
}
