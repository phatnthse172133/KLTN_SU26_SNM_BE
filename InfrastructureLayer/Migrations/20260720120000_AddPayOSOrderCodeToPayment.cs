using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

public partial class AddPayOSOrderCodeToPayment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // EditForPaymentAndOrder removed Gateway, while the current Payment model
        // and the PayOS idempotency index require it. Repair both fresh databases
        // and existing databases without overwriting an already populated column.
        migrationBuilder.Sql(
            """
            ALTER TABLE "Payments"
                ADD COLUMN IF NOT EXISTS "Gateway" character varying(20);

            UPDATE "Payments"
            SET "Gateway" = CASE
                WHEN "Type" = 'PayOS' THEN 'Payos'
                ELSE 'BankTransfer'
            END
            WHERE "Gateway" IS NULL OR "Gateway" = '';

            ALTER TABLE "Payments"
                ALTER COLUMN "Gateway" SET NOT NULL;
            """);

        migrationBuilder.AddColumn<long>(
            name: "PayOSOrderCode",
            table: "Payments",
            type: "bigint",
            nullable: true,
            comment: "PayOS order code for this specific payment transaction (supplemental payments)");

        migrationBuilder.CreateIndex(
            name: "idx_payments_payos_ordercode",
            table: "Payments",
            column: "PayOSOrderCode",
            unique: true,
            filter: "\"PayOSOrderCode\" IS NOT NULL");

        // Keep the newest usable pending PayOS payment per order before adding
        // the uniqueness constraint. Existing databases may contain duplicates
        // created before supplemental-payment idempotency was introduced.
        migrationBuilder.Sql(
            """
            WITH ranked AS (
                SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "OrderId"
                           ORDER BY
                               CASE WHEN NULLIF("CheckoutUrl", '') IS NOT NULL THEN 0 ELSE 1 END,
                               "CreatedAt" DESC,
                               "Id"
                       ) AS row_number
                FROM "Payments"
                WHERE "Status" = 'Pending' AND "Gateway" = 'Payos'
            )
            UPDATE "Payments" AS payment
            SET "Status" = 'Cancelled', "UpdatedAt" = NOW()
            FROM ranked
            WHERE payment."Id" = ranked."Id" AND ranked.row_number > 1;
            """);

        migrationBuilder.CreateIndex(
            name: "ux_payments_one_pending_payos_per_order",
            table: "Payments",
            column: "OrderId",
            unique: true,
            filter: "\"Status\" = 'Pending' AND \"Gateway\" = 'Payos'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_payments_one_pending_payos_per_order",
            table: "Payments");

        migrationBuilder.DropIndex(
            name: "idx_payments_payos_ordercode",
            table: "Payments");

        migrationBuilder.DropColumn(
            name: "PayOSOrderCode",
            table: "Payments");
    }
}
