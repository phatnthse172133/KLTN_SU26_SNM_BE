using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260727000000_FixNightMarketStatusDefault")]
public sealed class FixNightMarketStatusDefault : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
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
            oldDefaultValueSql: "'Active'::character varying");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "NightMarket",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValueSql: "'Active'::character varying",
            oldClrType: typeof(string),
            oldType: "character varying(20)",
            oldMaxLength: 20,
            oldDefaultValue: "Draft");
    }
}
