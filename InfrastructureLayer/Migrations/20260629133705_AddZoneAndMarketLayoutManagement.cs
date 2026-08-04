using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneAndMarketLayoutManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Zones_NightMarketId",
                table: "Zones");

            migrationBuilder.DropIndex(
                name: "IX_MarketLayouts_NightMarketId",
                table: "MarketLayouts");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Zones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MarketLayouts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LayoutName",
                table: "MarketLayouts",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "MarketLayouts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Draft'::character varying");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "MarketLayouts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "NightMarketId"
                               ORDER BY "CreatedAt", "Id") AS row_number
                    FROM "MarketLayouts"
                )
                UPDATE "MarketLayouts" AS layout
                SET "LayoutName" = 'Layout ' || ranked.row_number,
                    "Version" = ranked.row_number
                FROM ranked
                WHERE layout."Id" = ranked."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "ux_zone_market_name_active",
                table: "Zones",
                columns: new[] { "NightMarketId", "ZoneName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ux_marketlayout_market_name_active",
                table: "MarketLayouts",
                columns: new[] { "NightMarketId", "LayoutName" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ux_marketlayout_market_version_active",
                table: "MarketLayouts",
                columns: new[] { "NightMarketId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ux_marketlayout_one_active_per_market",
                table: "MarketLayouts",
                column: "NightMarketId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_zone_market_name_active",
                table: "Zones");

            migrationBuilder.DropIndex(
                name: "ux_marketlayout_market_name_active",
                table: "MarketLayouts");

            migrationBuilder.DropIndex(
                name: "ux_marketlayout_market_version_active",
                table: "MarketLayouts");

            migrationBuilder.DropIndex(
                name: "ux_marketlayout_one_active_per_market",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "LayoutName",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MarketLayouts");

            migrationBuilder.CreateIndex(
                name: "IX_Zones_NightMarketId",
                table: "Zones",
                column: "NightMarketId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketLayouts_NightMarketId",
                table: "MarketLayouts",
                column: "NightMarketId");
        }
    }
}
