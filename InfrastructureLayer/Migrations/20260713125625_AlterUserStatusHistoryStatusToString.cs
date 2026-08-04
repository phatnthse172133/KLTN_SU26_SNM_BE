using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AlterUserStatusHistoryStatusToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""UserStatusHistories""
ALTER COLUMN ""PreviousStatus"" TYPE varchar(20)
USING CASE ""PreviousStatus""
    WHEN 1 THEN 'Active'
    WHEN 4 THEN 'Inactive'
    ELSE NULL
END;");

            migrationBuilder.Sql(@"
ALTER TABLE ""UserStatusHistories""
ALTER COLUMN ""NewStatus"" TYPE varchar(20)
USING CASE ""NewStatus""
    WHEN 1 THEN 'Active'
    WHEN 4 THEN 'Inactive'
    ELSE NULL
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""UserStatusHistories""
ALTER COLUMN ""PreviousStatus"" TYPE integer
USING CASE ""PreviousStatus""
    WHEN 'Active' THEN 1
    WHEN 'Inactive' THEN 4
    ELSE 0
END;");

            migrationBuilder.Sql(@"
ALTER TABLE ""UserStatusHistories""
ALTER COLUMN ""NewStatus"" TYPE integer
USING CASE ""NewStatus""
    WHEN 'Active' THEN 1
    WHEN 'Inactive' THEN 4
    ELSE 0
END;");
        }
    }
}
