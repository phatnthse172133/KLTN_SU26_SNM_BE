using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserStatusValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ""User""
SET ""Status"" = CASE
    WHEN ""Status""::text IN ('Banned', 'Suspended') THEN 'Inactive'
    WHEN ""Status""::text IN ('Active', 'Inactive') THEN ""Status""
    ELSE 'Inactive'
END
WHERE ""Status""::text NOT IN ('Active', 'Inactive');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No reverse mapping needed; original values are lost after normalization.
        }
    }
}
