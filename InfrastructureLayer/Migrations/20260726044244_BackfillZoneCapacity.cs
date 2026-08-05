using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class BackfillZoneCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Zones""
                SET ""Capacity"" = COALESCE((
                    SELECT MAX(SlotCount)
                    FROM (
                        SELECT COUNT(*) AS SlotCount
                        FROM ""LayoutNodes""
                        WHERE ""NodeType"" = 'BoothSlot' AND ""ZoneId"" = ""Zones"".""Id""
                        GROUP BY ""LayoutId""
                    ) AS Subquery
                ), 0)
                WHERE ""Capacity"" = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
