using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileReviewComplaintFoodSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Complaints",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'Pending'::character varying",
                comment: "Pending | UnderReview | InProgress | WaitingForCustomer | Resolved | Rejected | Closed | Withdrawn",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValueSql: "'Pending'::character varying",
                oldComment: "Pending | UnderReview | WaitingForCustomer | Resolved | Rejected | Closed | Withdrawn");

            migrationBuilder.AlterColumn<string>(
                name: "AdminResponse",
                table: "Complaints",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoothOwnerResponse",
                table: "Complaints",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Complaints" SET "Category" = 'DelayedOrder' WHERE "Category" = 'OrderNotReceived';
                UPDATE "Complaints" SET "Category" = 'BoothBehavior' WHERE "Category" = 'BoothService';
                UPDATE "Complaints" SET "Category" = 'Other' WHERE "Category" = 'PromotionIssue';
                """);

            migrationBuilder.CreateIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints",
                columns: new[] { "CustomerId", "OrderId", "BoothId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'UnderReview', 'WaitingForCustomer', 'InProgress')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "BoothOwnerResponse",
                table: "Complaints");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Complaints",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'Pending'::character varying",
                comment: "Pending | UnderReview | WaitingForCustomer | Resolved | Rejected | Closed | Withdrawn",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValueSql: "'Pending'::character varying",
                oldComment: "Pending | UnderReview | InProgress | WaitingForCustomer | Resolved | Rejected | Closed | Withdrawn");

            migrationBuilder.AlterColumn<string>(
                name: "AdminResponse",
                table: "Complaints",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_complaint_active_customer_order_booth",
                table: "Complaints",
                columns: new[] { "CustomerId", "OrderId", "BoothId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'UnderReview', 'WaitingForCustomer')");
        }
    }
}
