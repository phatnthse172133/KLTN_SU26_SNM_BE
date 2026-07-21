using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class PackagePolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "NightMarket",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "NightMarket",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionReason",
                table: "NightMarket",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeType",
                table: "MarketSubscriptions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditAmount",
                table: "MarketSubscriptions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PolicyAcceptedAt",
                table: "MarketSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshotJson",
                table: "MarketSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "MarketSubscriptions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousSubscriptionId",
                table: "MarketSubscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeType",
                table: "BoothSubscriptions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditAmount",
                table: "BoothSubscriptions",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAt",
                table: "BoothSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PausedRemainingDays",
                table: "BoothSubscriptions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PolicyAcceptedAt",
                table: "BoothSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshotJson",
                table: "BoothSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "BoothSubscriptions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousSubscriptionId",
                table: "BoothSubscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackagePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    ContentMarkdown = table.Column<string>(type: "text", nullable: true),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackagePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackagePolicies_Package_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Package",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackagePolicies_PackageId_Version",
                table: "PackagePolicies",
                columns: new[] { "PackageId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackagePolicies");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "NightMarket");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "NightMarket");

            migrationBuilder.DropColumn(
                name: "DeletionReason",
                table: "NightMarket");

            migrationBuilder.DropColumn(
                name: "ChangeType",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "CreditAmount",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "PolicyAcceptedAt",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "PolicySnapshotJson",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "PreviousSubscriptionId",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "ChangeType",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "CreditAmount",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PausedRemainingDays",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PolicyAcceptedAt",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PolicySnapshotJson",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PreviousSubscriptionId",
                table: "BoothSubscriptions");
        }
    }
}
