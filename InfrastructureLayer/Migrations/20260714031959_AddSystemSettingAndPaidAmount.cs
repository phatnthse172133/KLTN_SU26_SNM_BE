using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettingAndPaidAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MarketOwnerId",
                table: "NightMarket",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "MarketSubscriptions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "BoothSubscriptions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SystemSetting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSetting", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NightMarket_MarketOwnerId",
                table: "NightMarket",
                column: "MarketOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSetting_Key",
                table: "SystemSetting",
                column: "Key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "NightMarket_MarketOwnerId_fkey",
                table: "NightMarket",
                column: "MarketOwnerId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "NightMarket_MarketOwnerId_fkey",
                table: "NightMarket");

            migrationBuilder.DropTable(
                name: "SystemSetting");

            migrationBuilder.DropIndex(
                name: "IX_NightMarket_MarketOwnerId",
                table: "NightMarket");

            migrationBuilder.DropColumn(
                name: "MarketOwnerId",
                table: "NightMarket");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "BoothSubscriptions");
        }
    }
}
