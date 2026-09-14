using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMapSectionsAndDefaultView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_marketlayout_market_version_active",
                table: "MarketLayouts");

            migrationBuilder.DropIndex(
                name: "ux_marketlayout_one_active_per_market",
                table: "MarketLayouts");

            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnLayoutId",
                table: "MarketLayouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "MarketLayouts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "MarketLayouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultView",
                table: "MarketLayouts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "OffsetXMeters",
                table: "MarketLayouts",
                type: "double precision",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OffsetYMeters",
                table: "MarketLayouts",
                type: "double precision",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "SectionCode",
                table: "MarketLayouts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "MAIN");

            migrationBuilder.AddColumn<string>(
                name: "SectionName",
                table: "MarketLayouts",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "Main Area");

            migrationBuilder.Sql("""
                UPDATE "MarketLayouts"
                SET "SectionCode" = 'MAIN',
                    "SectionName" = "LayoutName",
                    "IsDefaultView" = CASE WHEN "Status" = 'Active' THEN TRUE ELSE FALSE END
                WHERE "IsDeleted" = FALSE;
                """);
            migrationBuilder.CreateIndex(
                name: "ux_marketlayout_one_default_per_market",
                table: "MarketLayouts",
                column: "NightMarketId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 'Active' AND \"IsDefaultView\" = true");

            migrationBuilder.CreateIndex(
                name: "ux_marketlayout_one_published_per_section",
                table: "MarketLayouts",
                columns: new[] { "NightMarketId", "SectionCode" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ux_marketlayout_section_version_active",
                table: "MarketLayouts",
                columns: new[] { "NightMarketId", "SectionCode", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_marketlayout_one_default_per_market",
                table: "MarketLayouts");

            migrationBuilder.DropIndex(
                name: "ux_marketlayout_one_published_per_section",
                table: "MarketLayouts");

            migrationBuilder.DropIndex(
                name: "ux_marketlayout_section_version_active",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "BasedOnLayoutId",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "IsDefaultView",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "OffsetXMeters",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "OffsetYMeters",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "SectionCode",
                table: "MarketLayouts");

            migrationBuilder.DropColumn(
                name: "SectionName",
                table: "MarketLayouts");

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
    }
}
