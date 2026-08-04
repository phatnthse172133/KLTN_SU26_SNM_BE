using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationStatusAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add ModerationStatus column to NightMarket
            migrationBuilder.AddColumn<string>(
                name: "ModerationStatus",
                table: "NightMarket",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.Sql(@"
                ALTER TABLE ""NightMarket""
                ALTER COLUMN ""ModerationStatus"" SET DEFAULT 'Active'::character varying;
            ");

            migrationBuilder.CreateIndex(
                name: "idx_nightmarket_moderation_status",
                table: "NightMarket",
                column: "ModerationStatus");

            // Create ModerationActionHistory table
            migrationBuilder.CreateTable(
                name: "ModerationActionHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: true),
                    NightMarketId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PreviousStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'DirectAdmin'::character varying"),
                    ComplaintId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ModerationActionHistory_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "ModerationActionHistory_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "ModerationActionHistory_NightMarketId_fkey",
                        column: x => x.NightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_moderationhistory_booth",
                table: "ModerationActionHistory",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "idx_moderationhistory_nightmarket",
                table: "ModerationActionHistory",
                column: "NightMarketId");

            migrationBuilder.CreateIndex(
                name: "idx_moderationhistory_created",
                table: "ModerationActionHistory",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("ModerationActionHistory");

            migrationBuilder.DropIndex(
                name: "idx_nightmarket_moderation_status",
                table: "NightMarket");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                table: "NightMarket");
        }
    }
}
