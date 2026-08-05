using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// Additive-only storage for the physical layout generator.
/// Existing pixel layouts remain valid because every new column is nullable.
/// </summary>
[DbContext(typeof(SNMDbContext))]
[Migration("20260730090000_AddPhysicalLayoutGeometry")]
public partial class AddPhysicalLayoutGeometry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "MarketWidthMeters",
            table: "MarketLayouts",
            type: "double precision",
            nullable: true);
        migrationBuilder.AddColumn<double>(
            name: "MarketLengthMeters",
            table: "MarketLayouts",
            type: "double precision",
            nullable: true);
        migrationBuilder.AddColumn<double>(
            name: "PixelsPerMeter",
            table: "MarketLayouts",
            type: "double precision",
            nullable: true);

        migrationBuilder.AddColumn<double>(name: "WidthMeters", table: "Zones", type: "double precision", nullable: true);
        migrationBuilder.AddColumn<double>(name: "LengthMeters", table: "Zones", type: "double precision", nullable: true);
        migrationBuilder.AddColumn<double>(name: "BoothWidthMeters", table: "Zones", type: "double precision", nullable: true);
        migrationBuilder.AddColumn<double>(name: "BoothLengthMeters", table: "Zones", type: "double precision", nullable: true);
        migrationBuilder.AddColumn<double>(name: "HorizontalGapMeters", table: "Zones", type: "double precision", nullable: true);
        migrationBuilder.AddColumn<double>(name: "VerticalGapMeters", table: "Zones", type: "double precision", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MarketWidthMeters", table: "MarketLayouts");
        migrationBuilder.DropColumn(name: "MarketLengthMeters", table: "MarketLayouts");
        migrationBuilder.DropColumn(name: "PixelsPerMeter", table: "MarketLayouts");
        migrationBuilder.DropColumn(name: "WidthMeters", table: "Zones");
        migrationBuilder.DropColumn(name: "LengthMeters", table: "Zones");
        migrationBuilder.DropColumn(name: "BoothWidthMeters", table: "Zones");
        migrationBuilder.DropColumn(name: "BoothLengthMeters", table: "Zones");
        migrationBuilder.DropColumn(name: "HorizontalGapMeters", table: "Zones");
        migrationBuilder.DropColumn(name: "VerticalGapMeters", table: "Zones");
    }
}
