using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddNightMarketManagementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MapWidth",
                table: "NightMarket",
                newName: "BoundaryWidthMeters");

            migrationBuilder.RenameColumn(
                name: "MapHeight",
                table: "NightMarket",
                newName: "BoundaryHeightMeters");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "NightMarket",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "NightMarket",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "BoothPaymentInfos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Draft'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Active'::character varying");

            migrationBuilder.CreateIndex(
                name: "idx_nightmarket_active_status_created",
                table: "NightMarket",
                columns: new[] { "IsDeleted", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_nightmarket_active_status_created",
                table: "NightMarket");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "NightMarket");

            migrationBuilder.RenameColumn(
                name: "BoundaryWidthMeters",
                table: "NightMarket",
                newName: "MapWidth");

            migrationBuilder.RenameColumn(
                name: "BoundaryHeightMeters",
                table: "NightMarket",
                newName: "MapHeight");

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "NightMarket",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "BoothPaymentInfos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Active'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Draft'::character varying");
        }
    }
}
