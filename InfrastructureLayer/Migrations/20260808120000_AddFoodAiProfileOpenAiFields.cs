using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// Additive OpenAI enrichment columns on FoodAiProfile.
/// ContentHash remains the source fingerprint; AI fields are nullable.
/// </summary>
[DbContext(typeof(SNMDbContext))]
[Migration("20260808120000_AddFoodAiProfileOpenAiFields")]
public sealed class AddFoodAiProfileOpenAiFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AiDescription",
            table: "FoodAiProfile",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GeneratedByModel",
            table: "FoodAiProfile",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "Confidence",
            table: "FoodAiProfile",
            type: "numeric(5,4)",
            precision: 5,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StructuredProfileJson",
            table: "FoodAiProfile",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_foodaiprofile_status",
            table: "FoodAiProfile",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_foodaiprofile_status",
            table: "FoodAiProfile");

        migrationBuilder.DropColumn(name: "StructuredProfileJson", table: "FoodAiProfile");
        migrationBuilder.DropColumn(name: "Confidence", table: "FoodAiProfile");
        migrationBuilder.DropColumn(name: "GeneratedByModel", table: "FoodAiProfile");
        migrationBuilder.DropColumn(name: "AiDescription", table: "FoodAiProfile");
    }
}
