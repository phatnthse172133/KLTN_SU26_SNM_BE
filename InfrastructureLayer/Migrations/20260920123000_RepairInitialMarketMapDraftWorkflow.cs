using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(SNMDbContext))]
    [Migration("20260920123000_RepairInitialMarketMapDraftWorkflow")]
    public partial class RepairInitialMarketMapDraftWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MarketLayouts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Draft'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Inactive'::character varying");

            migrationBuilder.Sql(
                """
                UPDATE "MarketLayouts" AS layout
                SET "Status" = 'Draft'
                FROM "MarketMaps" AS map
                WHERE layout."MarketMapId" = map."Id"
                  AND layout."NightMarketId" = map."NightMarketId"
                  AND layout."Status" = 'Inactive'
                  AND layout."IsDeleted" = false
                  AND map."Status" = 'Draft';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MarketLayouts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Inactive'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Draft'::character varying");
        }
    }
}
