using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyBoothRegistrationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defensive drops: production DBs may use either EF-default or legacy constraint names.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Booth" DROP CONSTRAINT IF EXISTS "FK_Booth_BoothRegistrations_RegistrationId";
                ALTER TABLE "Booth" DROP CONSTRAINT IF EXISTS "Booth_RegistrationId_fkey";
                ALTER TABLE "BoothDocuments" DROP CONSTRAINT IF EXISTS "BoothDocuments_BoothId_fkey";
                ALTER TABLE "BoothDocuments" DROP CONSTRAINT IF EXISTS "BoothDocuments_RegistrationId_fkey";
                """);

            // Registration-only documents (never linked to a Booth) are legacy artifacts.
            migrationBuilder.Sql(
                """
                DELETE FROM "BoothDocuments"
                WHERE "BoothId" IS NULL AND "RegistrationId" IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "BoothRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_BoothDocuments_RegistrationId",
                table: "BoothDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Booth_RegistrationId",
                table: "Booth");

            migrationBuilder.DropColumn(
                name: "RegistrationId",
                table: "BoothDocuments");

            migrationBuilder.DropColumn(
                name: "RegistrationId",
                table: "Booth");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RegistrationId",
                table: "BoothDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegistrationId",
                table: "Booth",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BoothRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferredLayoutNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferredZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedNightMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    RejectReason = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoothRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoothRegistrations_LayoutNodes_PreferredLayoutNodeId",
                        column: x => x.PreferredLayoutNodeId,
                        principalTable: "LayoutNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BoothRegistrations_NightMarket_RequestedNightMarketId",
                        column: x => x.RequestedNightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoothRegistrations_User_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoothRegistrations_Zones_PreferredZoneId",
                        column: x => x.PreferredZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoothDocuments_RegistrationId",
                table: "BoothDocuments",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_Booth_RegistrationId",
                table: "Booth",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistrations_PreferredLayoutNodeId",
                table: "BoothRegistrations",
                column: "PreferredLayoutNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistrations_PreferredZoneId",
                table: "BoothRegistrations",
                column: "PreferredZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistrations_RequestedNightMarketId",
                table: "BoothRegistrations",
                column: "RequestedNightMarketId");

            migrationBuilder.CreateIndex(
                name: "uq_pending_booth_registration_owner",
                table: "BoothRegistrations",
                column: "OwnerId",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_Booth_BoothRegistrations_RegistrationId",
                table: "Booth",
                column: "RegistrationId",
                principalTable: "BoothRegistrations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "BoothDocuments_RegistrationId_fkey",
                table: "BoothDocuments",
                column: "RegistrationId",
                principalTable: "BoothRegistrations",
                principalColumn: "Id");
        }
    }
}
