using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrphanLegacyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'BoothRegistrations'
                          AND column_name = 'BoothId'
                    ) THEN
                        IF EXISTS (SELECT 1 FROM public."BoothRegistrations" WHERE "BoothId" IS NOT NULL) THEN
                            RAISE EXCEPTION 'Cleanup refused: BoothRegistrations.BoothId contains data.';
                        END IF;

                        ALTER TABLE public."BoothRegistrations"
                            DROP CONSTRAINT IF EXISTS "FK_BoothRegistrations_Booth_BoothId";
                        DROP INDEX IF EXISTS public."IX_BoothRegistrations_BoothId";
                        ALTER TABLE public."BoothRegistrations" DROP COLUMN "BoothId";
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Order'
                          AND column_name = 'PayStatus'
                    ) THEN
                        IF EXISTS (SELECT 1 FROM public."Order" WHERE "PayStatus" <> 0) THEN
                            RAISE EXCEPTION 'Cleanup refused: Order.PayStatus contains a non-legacy value.';
                        END IF;

                        ALTER TABLE public."Order" DROP COLUMN "PayStatus";
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BoothId",
                table: "BoothRegistrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoothRegistrations_BoothId",
                table: "BoothRegistrations",
                column: "BoothId");

            migrationBuilder.AddForeignKey(
                name: "FK_BoothRegistrations_Booth_BoothId",
                table: "BoothRegistrations",
                column: "BoothId",
                principalTable: "Booth",
                principalColumn: "Id");

            migrationBuilder.AddColumn<int>(
                name: "PayStatus",
                table: "Order",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
