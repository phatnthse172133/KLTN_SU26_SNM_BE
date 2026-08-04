using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Notification_BoothId_fkey",
                table: "Notification");

            migrationBuilder.AddColumn<string>(
                name: "DataJson",
                table: "Notification",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Notification",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRead",
                table: "Notification",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "Notification",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "Notification",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceType",
                table: "Notification",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserDeviceToken",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("UserDeviceToken_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "UserDeviceToken_UserId_fkey",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "FCM device tokens registered by users");

            migrationBuilder.CreateIndex(
                name: "idx_device_token_user_active",
                table: "UserDeviceToken",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UserDeviceToken_Token_key",
                table: "UserDeviceToken",
                column: "Token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "Notification_BoothId_fkey",
                table: "Notification",
                column: "BoothId",
                principalTable: "Booth",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Notification_BoothId_fkey",
                table: "Notification");

            migrationBuilder.DropTable(
                name: "UserDeviceToken");

            migrationBuilder.DropColumn(
                name: "DataJson",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "IsRead",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "ReferenceType",
                table: "Notification");

            migrationBuilder.AddForeignKey(
                name: "Notification_BoothId_fkey",
                table: "Notification",
                column: "BoothId",
                principalTable: "Booth",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
