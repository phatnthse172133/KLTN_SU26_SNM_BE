using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketLayoutDistanceCalibration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoordinateUnit",
                table: "MarketLayouts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'LayoutUnit'::character varying");

            migrationBuilder.AddColumn<string>(
                name: "DistanceCalibrationStatus",
                table: "MarketLayouts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'Uncalibrated'::character varying");

            migrationBuilder.AddColumn<decimal>(
                name: "MetersPerLayoutUnit",
                table: "MarketLayouts",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_marketlayout_positive_scale",
                table: "MarketLayouts",
                sql: "\"MetersPerLayoutUnit\" IS NULL OR \"MetersPerLayoutUnit\" > 0");

            // Legacy rows may contain invalid distances that cannot be repaired without
            // evidence. NOT VALID protects every new/updated row without inventing a backfill.
            migrationBuilder.Sql(
                """
                ALTER TABLE "LayoutEdges"
                ADD CONSTRAINT "ck_layoutedge_distance_positive"
                CHECK ("Distance" > 0) NOT VALID;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_marketlayout_positive_scale",
                table: "MarketLayouts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_layoutedge_distance_positive",
                table: "LayoutEdges");

            migrationBuilder.DropColumn(
                name: "CoordinateUnit",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "DistanceCalibrationStatus",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "MetersPerLayoutUnit",
                table: "MarketLayouts");
        }
    }
}
