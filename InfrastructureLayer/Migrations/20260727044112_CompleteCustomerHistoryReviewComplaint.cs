using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCustomerHistoryReviewComplaint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_CustomerId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_CustomerId",
                table: "Complaints");

            migrationBuilder.AddColumn<string>(
                name: "PromotionCodeSnapshot",
                table: "PromotionUsages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionTitleSnapshot",
                table: "PromotionUsages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FoodNameSnapshot",
                table: "OrderDetail",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Legacy rows can only be backfilled with the best currently available
            // names; they are not guaranteed to be the original historical values.
            migrationBuilder.Sql("""
                UPDATE "OrderDetail" AS od
                SET "FoodNameSnapshot" = COALESCE(NULLIF(f."Name", ''), 'Unknown item')
                FROM "FoodItem" AS f
                WHERE od."FoodItemId" = f."Id";

                UPDATE "OrderDetail"
                SET "FoodNameSnapshot" = 'Unknown item'
                WHERE "FoodNameSnapshot" IS NULL;

                UPDATE "PromotionUsages" AS pu
                SET "PromotionCodeSnapshot" = p."PromotionCode",
                    "PromotionTitleSnapshot" = COALESCE(NULLIF(p."Title", ''), 'Legacy promotion')
                FROM "Promotion" AS p
                WHERE pu."PromotionId" = p."Id";

                UPDATE "PromotionUsages"
                SET "PromotionTitleSnapshot" = 'Legacy promotion'
                WHERE "PromotionTitleSnapshot" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PromotionTitleSnapshot",
                table: "PromotionUsages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FoodNameSnapshot",
                table: "OrderDetail",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_reviews_booth_visible_created",
                table: "Reviews",
                columns: new[] { "BoothId", "IsVisible", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "idx_reviews_customer_created",
                table: "Reviews",
                columns: new[] { "CustomerId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "ck_reviews_rating",
                table: "Reviews",
                sql: "\"Rating\" BETWEEN 1 AND 5");

            migrationBuilder.Sql("""
                UPDATE "Booth" AS b
                SET "AverageRating" = COALESCE((
                    SELECT ROUND(AVG(r."Rating"::numeric), 2)
                    FROM "Reviews" AS r
                    WHERE r."BoothId" = b."Id" AND r."IsVisible" = TRUE
                ), 0)
                WHERE EXISTS (
                    SELECT 1 FROM "Reviews" AS existing WHERE existing."BoothId" = b."Id"
                );
                """);

            migrationBuilder.CreateIndex(
                name: "idx_order_customer_created",
                table: "Order",
                columns: new[] { "CustomerId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_order_customer_status_created",
                table: "Order",
                columns: new[] { "CustomerId", "Status", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "idx_complaint_customer_created",
                table: "Complaints",
                columns: new[] { "CustomerId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints",
                columns: new[] { "CustomerId", "OrderId", "BoothId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_reviews_booth_visible_created",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "idx_reviews_customer_created",
                table: "Reviews");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reviews_rating",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "idx_order_customer_created",
                table: "Order");

            migrationBuilder.DropIndex(
                name: "idx_order_customer_status_created",
                table: "Order");

            migrationBuilder.DropIndex(
                name: "idx_complaint_customer_created",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "PromotionCodeSnapshot",
                table: "PromotionUsages");

            migrationBuilder.DropColumn(
                name: "PromotionTitleSnapshot",
                table: "PromotionUsages");

            migrationBuilder.DropColumn(
                name: "FoodNameSnapshot",
                table: "OrderDetail");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CustomerId",
                table: "Reviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_CustomerId",
                table: "Complaints",
                column: "CustomerId");
        }
    }
}
