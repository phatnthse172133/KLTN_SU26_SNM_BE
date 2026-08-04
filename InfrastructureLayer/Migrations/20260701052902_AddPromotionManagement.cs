using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Promotion_BoothId",
                table: "Promotion",
                newName: "idx_promotion_booth");

            migrationBuilder.AddColumn<DateTime>(
                name: "AppliedAt",
                table: "PromotionUsages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "PromotionUsages",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                table: "PromotionUsages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PromotionUsages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Reserved'::character varying");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Promotion",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Scheduled'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Active'::character varying");

            migrationBuilder.Sql(
                """
                UPDATE "Promotion"
                SET "Status" = 'Scheduled'
                WHERE "Status" = 'Draft';
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Promotion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Promotion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximumDiscountAmount",
                table: "Promotion",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderAmount",
                table: "Promotion",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "Promotion",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "EntireBoothOrder");

            migrationBuilder.AddColumn<int>(
                name: "TotalUsageLimit",
                table: "Promotion",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsageLimitPerCustomer",
                table: "Promotion",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PromotionCategory",
                columns: table => new
                {
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PromotionCategory_pkey", x => new { x.PromotionId, x.CategoryId });
                    table.ForeignKey(
                        name: "PromotionCategory_CategoryId_fkey",
                        column: x => x.CategoryId,
                        principalTable: "FoodCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "PromotionCategory_PromotionId_fkey",
                        column: x => x.PromotionId,
                        principalTable: "Promotion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromotionFoodItem",
                columns: table => new
                {
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PromotionFoodItem_pkey", x => new { x.PromotionId, x.FoodItemId });
                    table.ForeignKey(
                        name: "PromotionFoodItem_FoodItemId_fkey",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "PromotionFoodItem_PromotionId_fkey",
                        column: x => x.PromotionId,
                        principalTable: "Promotion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_promotion_active_code",
                table: "Promotion",
                columns: new[] { "BoothId", "PromotionCode" },
                unique: true,
                filter: "\"PromotionCode\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "idx_promotioncategory_category",
                table: "PromotionCategory",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "idx_promotionfooditem_food",
                table: "PromotionFoodItem",
                column: "FoodItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromotionCategory");

            migrationBuilder.DropTable(
                name: "PromotionFoodItem");

            migrationBuilder.DropIndex(
                name: "ux_promotion_active_code",
                table: "Promotion");

            migrationBuilder.DropColumn(
                name: "AppliedAt",
                table: "PromotionUsages");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PromotionUsages");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "PromotionUsages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PromotionUsages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Promotion");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Promotion");

            migrationBuilder.DropColumn(
                name: "MaximumDiscountAmount",
                table: "Promotion");

            migrationBuilder.DropColumn(
                name: "MinimumOrderAmount",
                table: "Promotion");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "Promotion");

            migrationBuilder.DropColumn(
                name: "TotalUsageLimit",
                table: "Promotion");

            migrationBuilder.DropColumn(
                name: "UsageLimitPerCustomer",
                table: "Promotion");

            migrationBuilder.RenameIndex(
                name: "idx_promotion_booth",
                table: "Promotion",
                newName: "IX_Promotion_BoothId");

            migrationBuilder.Sql(
                """
                UPDATE "Promotion"
                SET "Status" = 'Draft'
                WHERE "Status" IN ('Scheduled', 'Inactive');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Promotion",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Active'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Scheduled'::character varying");

        }
    }
}
