using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_CinemaLayoutModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Booth_RegistrationId_fkey",
                table: "Booth");

            migrationBuilder.DropForeignKey(
                name: "BoothDocuments_BoothId_fkey",
                table: "BoothDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Booth_RegistrationId",
                table: "Booth");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ModerationActionHistory",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "uuid_generate_v4()");

            migrationBuilder.AlterColumn<string>(
                name: "PolicyVersion",
                table: "MarketSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CreditAmount",
                table: "MarketSubscriptions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "ChangeType",
                table: "MarketSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerPhone",
                table: "MarketSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerName",
                table: "MarketSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerEmail",
                table: "MarketSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ColumnIndex",
                table: "LayoutNodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LayoutBlockId",
                table: "LayoutNodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RowIndex",
                table: "LayoutNodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlotCode",
                table: "LayoutNodes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PolicyVersion",
                table: "BoothSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "CreditAmount",
                table: "BoothSubscriptions",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "ChangeType",
                table: "BoothSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerPhone",
                table: "BoothSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerName",
                table: "BoothSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BuyerEmail",
                table: "BoothSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BoothId",
                table: "BoothRegistrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LayoutBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    LayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    Width = table.Column<double>(type: "double precision", nullable: false),
                    Height = table.Column<double>(type: "double precision", nullable: false),
                    Rotation = table.Column<double>(type: "double precision", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigJson = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("LayoutBlocks_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "LayoutBlocks_LayoutId_fkey",
                        column: x => x.LayoutId,
                        principalTable: "MarketLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "LayoutBlocks_ZoneId_fkey",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Nhóm các block layout (dãy gian hàng, lối đi, khu vực, v.v.)");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutNodes_LayoutBlockId",
                table: "LayoutNodes",
                column: "LayoutBlockId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistrations_BoothId",
                table: "BoothRegistrations",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_Booth_RegistrationId",
                table: "Booth",
                column: "RegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutBlocks_LayoutId",
                table: "LayoutBlocks",
                column: "LayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutBlocks_ZoneId",
                table: "LayoutBlocks",
                column: "ZoneId");

            migrationBuilder.AddForeignKey(
                name: "Booth_RegistrationId_fkey",
                table: "Booth",
                column: "RegistrationId",
                principalTable: "BoothRegistrations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "BoothDocuments_RegistrationId_fkey",
                table: "BoothDocuments",
                column: "RegistrationId",
                principalTable: "BoothRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistrations_Booth_BoothId",
                table: "BoothRegistrations",
                column: "BoothId",
                principalTable: "Booth",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "LayoutNodes_LayoutBlockId_fkey",
                table: "LayoutNodes",
                column: "LayoutBlockId",
                principalTable: "LayoutBlocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "LayoutBlocks_LayoutId_fkey",
                table: "LayoutBlocks");

            migrationBuilder.DropForeignKey(
                name: "LayoutBlocks_ZoneId_fkey",
                table: "LayoutBlocks");

            migrationBuilder.DropTable(
                name: "LayoutBlocks");

            migrationBuilder.DropIndex(
                name: "IX_LayoutNodes_LayoutBlockId",
                table: "LayoutNodes");

            migrationBuilder.DropColumn(
                name: "ColumnIndex",
                table: "LayoutNodes");

            migrationBuilder.DropColumn(
                name: "LayoutBlockId",
                table: "LayoutNodes");

            migrationBuilder.DropColumn(
                name: "RowIndex",
                table: "LayoutNodes");

            migrationBuilder.DropColumn(
                name: "SlotCode",
                table: "LayoutNodes");
        }
    }
}
