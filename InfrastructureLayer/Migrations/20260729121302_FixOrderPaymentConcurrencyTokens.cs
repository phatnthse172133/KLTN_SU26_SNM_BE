using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class FixOrderPaymentConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Payments",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "uuid_send(gen_random_uuid())",
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Order",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "uuid_send(gen_random_uuid())",
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.snm_set_order_payment_row_version()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    NEW."RowVersion" := uuid_send(gen_random_uuid());
                    RETURN NEW;
                END;
                $$;

                UPDATE "Order" SET "RowVersion" = uuid_send(gen_random_uuid());
                UPDATE "Payments" SET "RowVersion" = uuid_send(gen_random_uuid());

                CREATE TRIGGER trg_order_row_version
                BEFORE UPDATE ON "Order"
                FOR EACH ROW EXECUTE FUNCTION public.snm_set_order_payment_row_version();

                CREATE TRIGGER trg_payments_row_version
                BEFORE UPDATE ON "Payments"
                FOR EACH ROW EXECUTE FUNCTION public.snm_set_order_payment_row_version();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_order_row_version ON "Order";
                DROP TRIGGER IF EXISTS trg_payments_row_version ON "Payments";
                DROP FUNCTION IF EXISTS public.snm_set_order_payment_row_version();
                """);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Payments",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldDefaultValueSql: "uuid_send(gen_random_uuid())");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Order",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea",
                oldRowVersion: true,
                oldDefaultValueSql: "uuid_send(gen_random_uuid())");
        }
    }
}
