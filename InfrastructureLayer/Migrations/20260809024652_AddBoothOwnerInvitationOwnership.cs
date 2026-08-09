using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddBoothOwnerInvitationOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMarketOwnerId",
                table: "User",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_User_CreatedByMarketOwnerId",
                table: "User",
                column: "CreatedByMarketOwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_User_User_CreatedByMarketOwnerId",
                table: "User",
                column: "CreatedByMarketOwnerId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_User_User_CreatedByMarketOwnerId",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_User_CreatedByMarketOwnerId",
                table: "User");

            migrationBuilder.DropColumn(
                name: "CreatedByMarketOwnerId",
                table: "User");
        }
    }
}
