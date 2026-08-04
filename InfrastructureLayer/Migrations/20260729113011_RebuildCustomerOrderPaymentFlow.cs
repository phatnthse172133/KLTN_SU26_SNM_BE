using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RebuildCustomerOrderPaymentFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "Payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureMessage",
                table: "Payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrCode",
                table: "Payments",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Payments",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "BoothId",
                table: "Order",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Order",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Order",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Order",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Order",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cash");

            migrationBuilder.AddColumn<Guid>(
                name: "PromotionId",
                table: "Order",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionSnapshot",
                table: "Order",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "Order",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Order",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql("""
                UPDATE "Order" AS o
                SET "BoothId" = source."BoothId"
                FROM (
                    SELECT od."OrderId", MIN(fi."BoothId"::text)::uuid AS "BoothId"
                    FROM "OrderDetail" AS od
                    INNER JOIN "FoodItem" AS fi ON fi."Id" = od."FoodItemId"
                    GROUP BY od."OrderId"
                ) AS source
                WHERE source."OrderId" = o."Id";

                UPDATE "Order" AS o
                SET "PaymentMethod" = COALESCE(
                    (SELECT p."Type" FROM "Payments" AS p WHERE p."OrderId" = o."Id" ORDER BY p."CreatedAt" LIMIT 1),
                    'Cash');

                UPDATE "Order"
                SET "IdempotencyKey" = "CheckoutRequestId"::text
                WHERE "CheckoutRequestId" IS NOT NULL;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Order" WHERE "BoothId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot migrate orders without a booth derived from their order details.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "BoothId",
                table: "Order",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    ProviderOrderCode = table.Column<long>(type: "bigint", nullable: false),
                    ProviderPaymentLinkId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CheckoutUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    QrCode = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAttempts_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProviderEventKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    SignatureHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO "PaymentAttempts"
                    ("Id", "PaymentId", "AttemptNumber", "ProviderOrderCode", "ProviderPaymentLinkId",
                     "Status", "CheckoutUrl", "QrCode", "CreatedAt", "UpdatedAt", "ExpiresAt")
                SELECT uuid_generate_v4(), p."Id", 1, p."PayOSOrderCode", p."PaymentLinkId",
                       CASE p."Status"
                           WHEN 'Paid' THEN 'Paid'
                           WHEN 'Cancelled' THEN 'Cancelled'
                           WHEN 'Failed' THEN 'Failed'
                           ELSE 'Pending'
                       END,
                       p."CheckoutUrl", p."QrCode", p."CreatedAt", p."UpdatedAt", p."ExpiresAt"
                FROM "Payments" AS p
                WHERE p."PayOSOrderCode" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Order_BoothId",
                table: "Order",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "ux_order_customer_idempotency_key",
                table: "Order",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_PaymentId_AttemptNumber",
                table: "PaymentAttempts",
                columns: new[] { "PaymentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_ProviderOrderCode",
                table: "PaymentAttempts",
                column: "ProviderOrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Provider_PayloadHash",
                table: "PaymentWebhookEvents",
                columns: new[] { "Provider", "PayloadHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Provider_ProviderEventKey",
                table: "PaymentWebhookEvents",
                columns: new[] { "Provider", "ProviderEventKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "Order_BoothId_fkey",
                table: "Order",
                column: "BoothId",
                principalTable: "Booth",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Order_BoothId_fkey",
                table: "Order");

            migrationBuilder.DropTable(
                name: "PaymentAttempts");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Order_BoothId",
                table: "Order");

            migrationBuilder.DropIndex(
                name: "ux_order_customer_idempotency_key",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FailureMessage",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "QrCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BoothId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PromotionSnapshot",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Order");
        }
    }
}
