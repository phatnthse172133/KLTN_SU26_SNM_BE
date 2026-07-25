using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeComplaintStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE ""Complaints""
SET ""Status"" = CASE
    WHEN ""Status"" IN ('Submitted', 'Open', 'UnderInvestigation', 'InProgress') THEN 'Pending'
    WHEN ""Status"" IN ('Closed') THEN 'Resolved'
    WHEN ""Status"" IN ('Resolved') THEN 'Resolved'
    WHEN ""Status"" IN ('Rejected') THEN 'Rejected'
    WHEN ""Status"" IN ('Pending') THEN 'Pending'
    ELSE 'Pending'
END;
");

            migrationBuilder.Sql(@"
ALTER TABLE ""Complaints"" ALTER COLUMN ""Status"" SET DEFAULT 'Pending'::character varying;
");

            migrationBuilder.Sql(@"
COMMENT ON COLUMN ""Complaints"".""Status"" IS 'Pending | Resolved | Rejected';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Complaints"" ALTER COLUMN ""Status"" SET DEFAULT 'Open'::character varying;
");

            migrationBuilder.Sql(@"
COMMENT ON COLUMN ""Complaints"".""Status"" IS 'Open | InProgress | Resolved | Rejected';
");
        }
    }
}
