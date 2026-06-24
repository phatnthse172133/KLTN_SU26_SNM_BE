using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booth_BoothRegistration_RegistrationId",
                table: "Booth");

            migrationBuilder.DropForeignKey(
                name: "FK_Booth_Zone_ZoneId",
                table: "Booth");

            migrationBuilder.DropForeignKey(
                name: "FK_BoothRegistration_LayoutNodes_PreferredLayoutNodeId",
                table: "BoothRegistration");

            migrationBuilder.DropForeignKey(
                name: "FK_BoothRegistration_NightMarket_RequestedNightMarketId",
                table: "BoothRegistration");

            migrationBuilder.DropForeignKey(
                name: "FK_BoothRegistration_User_OwnerId",
                table: "BoothRegistration");

            migrationBuilder.DropForeignKey(
                name: "FK_BoothRegistration_Zone_PreferredZoneId",
                table: "BoothRegistration");

            migrationBuilder.DropForeignKey(
                name: "FK_Zone_NightMarket_NightMarketId",
                table: "Zone");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Zone",
                table: "Zone");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BoothRegistration",
                table: "BoothRegistration");

            migrationBuilder.RenameTable(
                name: "Zone",
                newName: "Zones");

            migrationBuilder.RenameTable(
                name: "BoothRegistration",
                newName: "BoothRegistrations");

            migrationBuilder.RenameIndex(
                name: "IX_Zone_NightMarketId",
                table: "Zones",
                newName: "IX_Zones_NightMarketId");

            migrationBuilder.RenameIndex(
                name: "IX_BoothRegistration_RequestedNightMarketId",
                table: "BoothRegistrations",
                newName: "IX_BoothRegistrations_RequestedNightMarketId");

            migrationBuilder.RenameIndex(
                name: "IX_BoothRegistration_PreferredZoneId",
                table: "BoothRegistrations",
                newName: "IX_BoothRegistrations_PreferredZoneId");

            migrationBuilder.RenameIndex(
                name: "IX_BoothRegistration_PreferredLayoutNodeId",
                table: "BoothRegistrations",
                newName: "IX_BoothRegistrations_PreferredLayoutNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_BoothRegistration_OwnerId",
                table: "BoothRegistrations",
                newName: "IX_BoothRegistrations_OwnerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Zones",
                table: "Zones",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BoothRegistrations",
                table: "BoothRegistrations",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "EmailVerificationToken",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailVerificationToken_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshToken_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationToken_TokenHash",
                table: "EmailVerificationToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationToken_UserId_ExpiresAt",
                table: "EmailVerificationToken",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_TokenHash",
                table: "RefreshToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_UserId_ExpiresAt",
                table: "RefreshToken",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Booth_BoothRegistrations_RegistrationId",
                table: "Booth",
                column: "RegistrationId",
                principalTable: "BoothRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Booth_Zones_ZoneId",
                table: "Booth",
                column: "ZoneId",
                principalTable: "Zones",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistrations_LayoutNodes_PreferredLayoutNodeId",
                table: "BoothRegistrations",
                column: "PreferredLayoutNodeId",
                principalTable: "LayoutNodes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistrations_NightMarket_RequestedNightMarketId",
                table: "BoothRegistrations",
                column: "RequestedNightMarketId",
                principalTable: "NightMarket",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistrations_User_OwnerId",
                table: "BoothRegistrations",
                column: "OwnerId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistrations_Zones_PreferredZoneId",
                table: "BoothRegistrations",
                column: "PreferredZoneId",
                principalTable: "Zones",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Zones_NightMarket_NightMarketId",
                table: "Zones",
                column: "NightMarketId",
                principalTable: "NightMarket",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booth_BoothRegistrations_RegistrationId",
                table: "Booth");

            migrationBuilder.DropForeignKey(
                name: "FK_Booth_Zones_ZoneId",
                table: "Booth");

            migrationBuilder.DropForeignKey(
                name: "FK_BoothRegistrations_LayoutNodes_PreferredLayoutNodeId",
                table: "BoothRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_BoothRegistrations_NightMarket_RequestedNightMarketId",
                table: "BoothRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_BoothRegistrations_User_OwnerId",
                table: "BoothRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_BoothRegistrations_Zones_PreferredZoneId",
                table: "BoothRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_Zones_NightMarket_NightMarketId",
                table: "Zones");

            migrationBuilder.DropTable(
                name: "EmailVerificationToken");

            migrationBuilder.DropTable(
                name: "RefreshToken");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Zones",
                table: "Zones");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BoothRegistrations",
                table: "BoothRegistrations");

            migrationBuilder.RenameTable(
                name: "Zones",
                newName: "Zone");

            migrationBuilder.RenameTable(
                name: "BoothRegistrations",
                newName: "BoothRegistration");

            migrationBuilder.RenameIndex(
                name: "IX_Zones_NightMarketId",
                table: "Zone",
                newName: "IX_Zone_NightMarketId");

            migrationBuilder.RenameIndex(
                name: "IX_BoothRegistrations_RequestedNightMarketId",
                table: "BoothRegistration",
                newName: "IX_BoothRegistration_RequestedNightMarketId");

            migrationBuilder.RenameIndex(
                name: "IX_BoothRegistrations_PreferredZoneId",
                table: "BoothRegistration",
                newName: "IX_BoothRegistration_PreferredZoneId");

            migrationBuilder.RenameIndex(
                name: "IX_BoothRegistrations_PreferredLayoutNodeId",
                table: "BoothRegistration",
                newName: "IX_BoothRegistration_PreferredLayoutNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_BoothRegistrations_OwnerId",
                table: "BoothRegistration",
                newName: "IX_BoothRegistration_OwnerId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Zone",
                table: "Zone",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BoothRegistration",
                table: "BoothRegistration",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Booth_BoothRegistration_RegistrationId",
                table: "Booth",
                column: "RegistrationId",
                principalTable: "BoothRegistration",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Booth_Zone_ZoneId",
                table: "Booth",
                column: "ZoneId",
                principalTable: "Zone",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistration_LayoutNodes_PreferredLayoutNodeId",
                table: "BoothRegistration",
                column: "PreferredLayoutNodeId",
                principalTable: "LayoutNodes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistration_NightMarket_RequestedNightMarketId",
                table: "BoothRegistration",
                column: "RequestedNightMarketId",
                principalTable: "NightMarket",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistration_User_OwnerId",
                table: "BoothRegistration",
                column: "OwnerId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistration_Zone_PreferredZoneId",
                table: "BoothRegistration",
                column: "PreferredZoneId",
                principalTable: "Zone",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Zone_NightMarket_NightMarketId",
                table: "Zone",
                column: "NightMarketId",
                principalTable: "NightMarket",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
