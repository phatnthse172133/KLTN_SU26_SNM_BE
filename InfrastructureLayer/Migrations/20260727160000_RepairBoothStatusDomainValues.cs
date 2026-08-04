using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260727160000_RepairBoothStatusDomainValues")]
public sealed class RepairBoothStatusDomainValues : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Booth"
            SET "Status" = CASE
                WHEN "Status" = 'Suspended' THEN 'Banned'
                WHEN "Status" IN ('Active', 'Inactive', 'Banned') THEN "Status"
                ELSE 'Inactive'
            END;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Booth",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            comment: "Active | Inactive | Banned",
            oldClrType: typeof(string),
            oldType: "character varying(20)",
            oldMaxLength: 20,
            oldDefaultValueSql: "'Pending'::character varying",
            oldComment: "Pending | Active | Inactive | Suspended");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Booth"
            SET "Status" = 'Suspended'
            WHERE "Status" = 'Banned';
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Booth",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValueSql: "'Pending'::character varying",
            comment: "Pending | Active | Inactive | Suspended",
            oldClrType: typeof(string),
            oldType: "character varying(20)",
            oldMaxLength: 20,
            oldComment: "Active | Inactive | Banned");
    }
}
