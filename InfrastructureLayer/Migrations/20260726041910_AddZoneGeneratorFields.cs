using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneGeneratorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Zones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "DefaultBoothHeight",
                table: "Zones",
                type: "double precision",
                nullable: false,
                defaultValue: 60.0);

            migrationBuilder.AddColumn<double>(
                name: "DefaultBoothWidth",
                table: "Zones",
                type: "double precision",
                nullable: false,
                defaultValue: 80.0);

            migrationBuilder.AddColumn<double>(
                name: "DefaultGap",
                table: "Zones",
                type: "double precision",
                nullable: false,
                defaultValue: 20.0);

            migrationBuilder.AddColumn<string>(
                name: "ZoneCode",
                table: "Zones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_zone_market_code_active",
                table: "Zones",
                columns: new[] { "NightMarketId", "ZoneCode" },
                unique: true,
                filter: "\"ZoneCode\" IS NOT NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_zone_market_code_active",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "DefaultBoothHeight",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "DefaultBoothWidth",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "DefaultGap",
                table: "Zones");

            migrationBuilder.DropColumn(
                name: "ZoneCode",
                table: "Zones");
        }
    }
}
