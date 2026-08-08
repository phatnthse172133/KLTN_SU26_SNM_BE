using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class ExpandReviewComplaintCustomerSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints");

            migrationBuilder.AlterTable(
                name: "Complaints",
                comment: "Khiếu nại của khách hàng về đơn hàng/gian hàng",
                oldComment: "Khiáº¿u náº¡i cá»§a khÃ¡ch hÃ ng vá» Ä‘Æ¡n hÃ ng/gian hÃ ng");

            migrationBuilder.AlterTable(
                name: "ComplaintImages",
                comment: "Ảnh minh chứng đính kèm theo khiếu nại",
                oldComment: "Ã¡ÂºÂ¢nh minh chÃ¡Â»Â©ng Ã„â€˜ÃƒÂ­nh kÃƒÂ¨m theo khiÃ¡ÂºÂ¿u nÃ¡ÂºÂ¡i");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "FoodItem",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                comment: "Giá mặc định. Nếu có FoodPrice theo ngày hiện tại thì giá đó được ưu tiên (override)",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldComment: "GiÃƒÂ¡ mÃ¡ÂºÂ·c Ã„â€˜Ã¡Â»â€¹nh. NÃ¡ÂºÂ¿u cÃƒÂ³ FoodPrice theo ngÃƒÂ y hiÃ¡Â»â€¡n tÃ¡ÂºÂ¡i thÃƒÂ¬ giÃƒÂ¡ Ã„â€˜ÃƒÂ³ Ã„â€˜Ã†Â°Ã¡Â»Â£c Ã†Â°u tiÃƒÂªn (override)");

            migrationBuilder.AddColumn<decimal>(
                name: "AverageRating",
                table: "FoodItem",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValueSql: "0");

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "FoodItem",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Complaints",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'Pending'::character varying",
                comment: "Pending | UnderReview | WaitingForCustomer | Resolved | Rejected | Closed | Withdrawn",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Pending'::character varying",
                oldComment: "Pending | Resolved | Rejected");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Complaints",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'Other'::character varying");

            migrationBuilder.AddColumn<string>(
                name: "CustomerEvidenceRequestNote",
                table: "Complaints",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComplaintStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ComplaintId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ComplaintStatusHistories_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "ComplaintStatusHistories_ComplaintId_fkey",
                        column: x => x.ComplaintId,
                        principalTable: "Complaints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    OrderDetailId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<short>(type: "smallint", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FoodReviews_pkey", x => x.Id);
                    table.CheckConstraint("ck_foodreviews_rating", "\"Rating\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FoodReviews_BoothId_fkey",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FoodReviews_CustomerId_fkey",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FoodReviews_FoodItemId_fkey",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FoodReviews_OrderDetailId_fkey",
                        column: x => x.OrderDetailId,
                        principalTable: "OrderDetail",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FoodReviews_OrderId_fkey",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints",
                columns: new[] { "CustomerId", "OrderId", "BoothId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'UnderReview', 'WaitingForCustomer')");

            migrationBuilder.CreateIndex(
                name: "idx_complaintstatushistory_complaint_created",
                table: "ComplaintStatusHistories",
                columns: new[] { "ComplaintId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "idx_foodreviews_customer_created",
                table: "FoodReviews",
                columns: new[] { "CustomerId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_foodreviews_fooditem_visible_created",
                table: "FoodReviews",
                columns: new[] { "FoodItemId", "IsVisible", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_FoodReviews_BoothId",
                table: "FoodReviews",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodReviews_OrderId",
                table: "FoodReviews",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "uq_foodreview_orderdetail",
                table: "FoodReviews",
                column: "OrderDetailId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplaintStatusHistories");

            migrationBuilder.DropTable(
                name: "FoodReviews");

            migrationBuilder.DropIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "FoodItem");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "FoodItem");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "CustomerEvidenceRequestNote",
                table: "Complaints");

            migrationBuilder.AlterTable(
                name: "Complaints",
                comment: "Khiáº¿u náº¡i cá»§a khÃ¡ch hÃ ng vá» Ä‘Æ¡n hÃ ng/gian hÃ ng",
                oldComment: "Khiếu nại của khách hàng về đơn hàng/gian hàng");

            migrationBuilder.AlterTable(
                name: "ComplaintImages",
                comment: "Ã¡ÂºÂ¢nh minh chÃ¡Â»Â©ng Ã„â€˜ÃƒÂ­nh kÃƒÂ¨m theo khiÃ¡ÂºÂ¿u nÃ¡ÂºÂ¡i",
                oldComment: "Ảnh minh chứng đính kèm theo khiếu nại");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "FoodItem",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                comment: "GiÃƒÂ¡ mÃ¡ÂºÂ·c Ã„â€˜Ã¡Â»â€¹nh. NÃ¡ÂºÂ¿u cÃƒÂ³ FoodPrice theo ngÃƒÂ y hiÃ¡Â»â€¡n tÃ¡ÂºÂ¡i thÃƒÂ¬ giÃƒÂ¡ Ã„â€˜ÃƒÂ³ Ã„â€˜Ã†Â°Ã¡Â»Â£c Ã†Â°u tiÃƒÂªn (override)",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldComment: "Giá mặc định. Nếu có FoodPrice theo ngày hiện tại thì giá đó được ưu tiên (override)");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Complaints",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Pending'::character varying",
                comment: "Pending | Resolved | Rejected",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValueSql: "'Pending'::character varying",
                oldComment: "Pending | UnderReview | WaitingForCustomer | Resolved | Rejected | Closed | Withdrawn");

            migrationBuilder.CreateIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints",
                columns: new[] { "CustomerId", "OrderId", "BoothId" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }
    }
}
