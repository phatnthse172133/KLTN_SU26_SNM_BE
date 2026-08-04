using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeNightMarketOperationalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "NightMarket"
                SET "ModerationStatus" = 'Suspended',
                    "Status" = 'Inactive'
                WHERE "Status" = 'Suspended';

                UPDATE "NightMarket"
                SET "Status" = 'Active'
                WHERE "Status" = 'Open';

                UPDATE "NightMarket"
                SET "Status" = 'Inactive'
                WHERE "Status" IN ('Draft', 'Upcoming', 'Closed', 'Cancelled');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
