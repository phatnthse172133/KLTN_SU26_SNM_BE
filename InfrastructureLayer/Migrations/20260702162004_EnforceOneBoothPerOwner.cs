using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOneBoothPerOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_booth_owner",
                table: "Booth");

            migrationBuilder.DropIndex(
                name: "IX_BoothRegistrations_OwnerId",
                table: "BoothRegistrations");

            migrationBuilder.CreateIndex(
                name: "uq_booth_owner",
                table: "Booth",
                column: "BoothOwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_pending_booth_registration_owner",
                table: "BoothRegistrations",
                column: "OwnerId",
                unique: true,
                filter: "\"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_booth_owner",
                table: "Booth");

            migrationBuilder.DropIndex(
                name: "uq_pending_booth_registration_owner",
                table: "BoothRegistrations");

            migrationBuilder.CreateIndex(
                name: "idx_booth_owner",
                table: "Booth",
                column: "BoothOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistrations_OwnerId",
                table: "BoothRegistrations",
                column: "OwnerId");
        }
    }
}
