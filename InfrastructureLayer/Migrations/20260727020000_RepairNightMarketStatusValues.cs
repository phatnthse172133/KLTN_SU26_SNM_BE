using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260727020000_RepairNightMarketStatusValues")]
public sealed class RepairNightMarketStatusValues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "NightMarket"
            SET "Status" = CASE
                WHEN "Status" IN ('Active', 'Open', 'Upcoming') THEN 'Active'
                ELSE 'Inactive'
            END;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "NightMarket",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Inactive",
            oldClrType: typeof(string),
            oldType: "character varying(20)",
            oldMaxLength: 20,
            oldDefaultValue: "Draft");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "NightMarket",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Draft",
            oldClrType: typeof(string),
            oldType: "character varying(20)",
            oldMaxLength: 20,
            oldDefaultValue: "Inactive");
    }
}
