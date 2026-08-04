using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RepairUserStatusHistoryStatusColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""UserStatusHistories""
ALTER COLUMN ""PreviousStatus"" TYPE varchar(20)
USING CASE
    WHEN ""PreviousStatus""::text IN ('1', 'Active') THEN 'Active'
    WHEN ""PreviousStatus""::text IN ('2', '4', 'Inactive') THEN 'Inactive'
    ELSE 'Inactive'
END;
");

            migrationBuilder.Sql(@"
ALTER TABLE ""UserStatusHistories""
ALTER COLUMN ""NewStatus"" TYPE varchar(20)
USING CASE
    WHEN ""NewStatus""::text IN ('1', 'Active') THEN 'Active'
    WHEN ""NewStatus""::text IN ('2', '4', 'Inactive') THEN 'Inactive'
    ELSE 'Inactive'
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""UserStatusHistories""
ALTER COLUMN ""PreviousStatus"" TYPE integer
USING CASE
    WHEN ""PreviousStatus"" = 'Active' THEN 1
    WHEN ""PreviousStatus"" = 'Inactive' THEN 2
    ELSE 2
END;
");

            migrationBuilder.Sql(@"
ALTER TABLE ""UserStatusHistories""
ALTER COLUMN ""NewStatus"" TYPE integer
USING CASE
    WHEN ""NewStatus"" = 'Active' THEN 1
    WHEN ""NewStatus"" = 'Inactive' THEN 2
    ELSE 2
END;
");
        }
    }
}
