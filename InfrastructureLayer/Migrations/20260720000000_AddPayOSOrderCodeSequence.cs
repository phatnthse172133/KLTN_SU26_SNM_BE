using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPayOSOrderCodeSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the sequence used by PayOSOrderCodeGenerator via ISequenceRepository.NextPayOSOrderCodeAsync().
            // MAXVALUE 99999999999999 (14 digits) ensures the sequence never overflows into the prefix region
            // (prefix * 100_000_000_000_000 + seqValue). NO CYCLE prevents wrap-around collisions.
            migrationBuilder.Sql(@"
                CREATE SEQUENCE IF NOT EXISTS payos_order_code_seq
                    INCREMENT 1
                    START 1
                    MINVALUE 1
                    MAXVALUE 99999999999999
                    NO CYCLE;
            ");

            // Sync the sequence with the largest suffix already in use across all three domains:
            //   - Order."OrderCode"        (prefix 1: code = 1 * 10^14 + suffix)
            //   - BoothSubscriptions."PayOSOrderCode"  (prefix 2)
            //   - MarketSubscriptions."PayOSOrderCode" (prefix 3)
            //
            // We extract the suffix (code % 10^14) from every domain, take the global MAX,
            // and setval so the next nextval() is strictly greater than any existing code suffix.
            migrationBuilder.Sql(@"
                SELECT setval(
                    'payos_order_code_seq',
                    GREATEST(
                        1,
                        COALESCE(
                            (SELECT MAX(""OrderCode"" % 100000000000000)
                             FROM ""Order""
                             WHERE ""OrderCode"" >= 100000000000000
                               AND ""OrderCode"" <  200000000000000),
                            0
                        ),
                        COALESCE(
                            (SELECT MAX(""PayOSOrderCode"" % 100000000000000)
                             FROM ""BoothSubscriptions""
                             WHERE ""PayOSOrderCode"" IS NOT NULL
                               AND ""PayOSOrderCode"" >= 200000000000000
                               AND ""PayOSOrderCode"" <  300000000000000),
                            0
                        ),
                        COALESCE(
                            (SELECT MAX(""PayOSOrderCode"" % 100000000000000)
                             FROM ""MarketSubscriptions""
                             WHERE ""PayOSOrderCode"" IS NOT NULL
                               AND ""PayOSOrderCode"" >= 300000000000000
                               AND ""PayOSOrderCode"" <  400000000000000),
                            0
                        )
                    )
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS payos_order_code_seq;");
        }
    }
}
