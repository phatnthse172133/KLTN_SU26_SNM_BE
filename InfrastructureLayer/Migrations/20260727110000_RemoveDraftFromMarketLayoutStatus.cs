using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

public partial class RemoveDraftFromMarketLayoutStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Migrate existing Draft layouts to Inactive
        migrationBuilder.Sql(@"UPDATE ""MarketLayouts"" SET ""Status"" = 'Inactive' WHERE ""Status"" = 'Draft';");

        // 2. Update the column default from 'Draft' to 'Inactive'
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "MarketLayouts",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValueSql: "'Inactive'::character varying",
            oldDefaultValueSql: "'Draft'::character varying");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Revert: Inactive back to Draft for rows that were originally Draft (best-effort)
        // Note: This is a lossy downgrade — we cannot distinguish original Inactive from migrated Draft.
        // Only revert the column default.
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "MarketLayouts",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValueSql: "'Draft'::character varying",
            oldDefaultValueSql: "'Inactive'::character varying");
    }
}
