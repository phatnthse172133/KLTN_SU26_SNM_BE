using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerDetailsToSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "MarketSubscriptions"
                    ADD COLUMN IF NOT EXISTS "BuyerName" character varying(200),
                    ADD COLUMN IF NOT EXISTS "BuyerEmail" character varying(200),
                    ADD COLUMN IF NOT EXISTS "BuyerPhone" character varying(20);

                ALTER TABLE "BoothSubscriptions"
                    ADD COLUMN IF NOT EXISTS "BuyerName" character varying(200),
                    ADD COLUMN IF NOT EXISTS "BuyerEmail" character varying(200),
                    ADD COLUMN IF NOT EXISTS "BuyerPhone" character varying(20);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "MarketSubscriptions"
                    DROP COLUMN IF EXISTS "BuyerName",
                    DROP COLUMN IF EXISTS "BuyerEmail",
                    DROP COLUMN IF EXISTS "BuyerPhone";

                ALTER TABLE "BoothSubscriptions"
                    DROP COLUMN IF EXISTS "BuyerName",
                    DROP COLUMN IF EXISTS "BuyerEmail",
                    DROP COLUMN IF EXISTS "BuyerPhone";
                """);
        }
    }
}
