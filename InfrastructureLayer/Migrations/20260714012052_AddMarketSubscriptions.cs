using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Package",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "BoothSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentEvidenceUrl",
                table: "BoothSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    MarketOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Active'::character varying", comment: "Active | Expired | Cancelled | PendingPayment"),
                    PaymentEvidenceUrl = table.Column<string>(type: "text", nullable: true),
                    AdminNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("MarketSubscriptions_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "MarketSubscriptions_MarketOwnerId_fkey",
                        column: x => x.MarketOwnerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "MarketSubscriptions_PackageId_fkey",
                        column: x => x.PackageId,
                        principalTable: "Package",
                        principalColumn: "Id");
                },
                comment: "Lịch sử đăng ký gói dịch vụ của Market Owner");

            migrationBuilder.CreateIndex(
                name: "IX_MarketSubscriptions_MarketOwnerId",
                table: "MarketSubscriptions",
                column: "MarketOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketSubscriptions_PackageId",
                table: "MarketSubscriptions",
                column: "PackageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Package");

            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentEvidenceUrl",
                table: "BoothSubscriptions");
        }
    }
}
