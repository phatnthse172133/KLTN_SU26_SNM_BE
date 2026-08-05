using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeBoothStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Booth"
                SET "Status" = 'Banned'
                WHERE "Status" = 'Suspended';

                UPDATE "Booth"
                SET "Status" = 'Inactive'
                WHERE "Status" IN ('Pending', 'PendingApproval', 'Closed');

                UPDATE "ModerationActionHistory"
                SET "PreviousStatus" = 'Banned'
                WHERE "BoothId" IS NOT NULL AND "PreviousStatus" = 'Suspended';

                UPDATE "ModerationActionHistory"
                SET "PreviousStatus" = 'Inactive'
                WHERE "BoothId" IS NOT NULL AND "PreviousStatus" IN ('Pending', 'PendingApproval', 'Closed');

                UPDATE "ModerationActionHistory"
                SET "NewStatus" = 'Banned'
                WHERE "BoothId" IS NOT NULL AND "NewStatus" = 'Suspended';

                UPDATE "ModerationActionHistory"
                SET "NewStatus" = 'Inactive'
                WHERE "BoothId" IS NOT NULL AND "NewStatus" IN ('Pending', 'PendingApproval', 'Closed');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Booth",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Inactive'::character varying",
                comment: "Active | Inactive | Banned",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Pending'::character varying",
                oldComment: "Pending: chờ Admin duyệt | Active: hoạt động | Inactive: tạm ngừng | Suspended: bị khóa do vi phạm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Booth",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Pending'::character varying",
                comment: "Pending: chờ Admin duyệt | Active: hoạt động | Inactive: tạm ngừng | Suspended: bị khóa do vi phạm",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Inactive'::character varying",
                oldComment: "Active | Inactive | Banned");
        }
    }
}
