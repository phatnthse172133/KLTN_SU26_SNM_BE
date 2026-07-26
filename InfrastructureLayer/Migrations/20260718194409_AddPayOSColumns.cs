using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPayOSColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
            ALTER TABLE "MarketSubscriptions"
                ADD COLUMN IF NOT EXISTS "PaidAt" timestamp with time zone;
            ALTER TABLE "MarketSubscriptions"
                ADD COLUMN IF NOT EXISTS "PayOSOrderCode" bigint;
            ALTER TABLE "MarketSubscriptions"
                ADD COLUMN IF NOT EXISTS "PayOSPaymentLinkId" character varying(100);
            ALTER TABLE "MarketSubscriptions"
                ADD COLUMN IF NOT EXISTS "PaymentExpiresAt" timestamp with time zone;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE "BoothSubscriptions"
                ADD COLUMN IF NOT EXISTS "PaidAt" timestamp with time zone;
            ALTER TABLE "BoothSubscriptions"
                ADD COLUMN IF NOT EXISTS "PayOSOrderCode" bigint;
            ALTER TABLE "BoothSubscriptions"
                ADD COLUMN IF NOT EXISTS "PayOSPaymentLinkId" character varying(100);
            ALTER TABLE "BoothSubscriptions"
                ADD COLUMN IF NOT EXISTS "PaymentExpiresAt" timestamp with time zone;
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_MarketSubscriptions_PayOSOrderCode"
                ON "MarketSubscriptions" ("PayOSOrderCode") WHERE "PayOSOrderCode" IS NOT NULL;
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_BoothSubscriptions_PayOSOrderCode"
                ON "BoothSubscriptions" ("PayOSOrderCode") WHERE "PayOSOrderCode" IS NOT NULL;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketSubscriptions_PayOSOrderCode",
                table: "MarketSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_BoothSubscriptions_PayOSOrderCode",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "PayOSOrderCode",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "PayOSPaymentLinkId",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "MarketSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PayOSOrderCode",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PayOSPaymentLinkId",
                table: "BoothSubscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "BoothSubscriptions");
        }
    }
}
