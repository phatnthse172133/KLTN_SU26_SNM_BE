using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationHistoryComplaintId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older databases predate the ComplaintId column that the table-creation
            // migration now declares, so guard against both states.
            migrationBuilder.Sql("""
                ALTER TABLE "ModerationActionHistory" ADD COLUMN IF NOT EXISTS "ComplaintId" uuid;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComplaintId",
                table: "ModerationActionHistory");
        }
    }
}
