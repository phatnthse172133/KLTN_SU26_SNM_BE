using System;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260717113421_AddAdminUserStatusManagement")]
public partial class AddAdminUserStatusManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "User"
            SET "Status" = CASE
                WHEN "Status"::text IN ('Banned', 'Suspended') THEN 'Inactive'
                WHEN "Status"::text IN ('Active', 'Inactive') THEN "Status"
                ELSE 'Inactive'
            END
            WHERE "Status"::text NOT IN ('Active', 'Inactive');
            """);

        migrationBuilder.CreateTable(
            name: "EmailOutbox",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RecipientEmail = table.Column<string>(type: "text", nullable: false),
                Subject = table.Column<string>(type: "text", nullable: false),
                HtmlBody = table.Column<string>(type: "text", nullable: false),
                EmailType = table.Column<string>(type: "text", nullable: false),
                ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                RetryCount = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "text", nullable: true),
                NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmailOutbox", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserStatusHistories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ChangedByAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviousStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                NewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserStatusHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserStatusHistories_User_ChangedByAdminId",
                    column: x => x.ChangedByAdminId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_UserStatusHistories_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EmailOutbox_ReferenceId_EmailType",
            table: "EmailOutbox",
            columns: new[] { "ReferenceId", "EmailType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserStatusHistories_ChangedByAdminId",
            table: "UserStatusHistories",
            column: "ChangedByAdminId");

        migrationBuilder.CreateIndex(
            name: "IX_UserStatusHistories_UserId_CreatedAt",
            table: "UserStatusHistories",
            columns: new[] { "UserId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EmailOutbox");
        migrationBuilder.DropTable(name: "UserStatusHistories");
    }
}
