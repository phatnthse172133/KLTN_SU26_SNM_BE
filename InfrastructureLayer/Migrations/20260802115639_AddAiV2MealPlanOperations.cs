using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddAiV2MealPlanOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "AiMealPlanSession",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "AiMealPlanSession",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsedProviderFallback",
                table: "AiMealPlanSession",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WarningsJson",
                table: "AiMealPlanSession",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrlSnapshot",
                table: "AiMealPlanItem",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RatingSnapshot",
                table: "AiMealPlanItem",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCountSnapshot",
                table: "AiMealPlanItem",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "AiMealPlan",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarningsJson",
                table: "AiMealPlan",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_aimealplansession_customer_idempotency",
                table: "AiMealPlanSession",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true,
                filter: "\"CustomerId\" IS NOT NULL AND \"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_aimealplansession_customer_idempotency",
                table: "AiMealPlanSession");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "AiMealPlanSession");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "AiMealPlanSession");

            migrationBuilder.DropColumn(
                name: "UsedProviderFallback",
                table: "AiMealPlanSession");

            migrationBuilder.DropColumn(
                name: "WarningsJson",
                table: "AiMealPlanSession");

            migrationBuilder.DropColumn(
                name: "ImageUrlSnapshot",
                table: "AiMealPlanItem");

            migrationBuilder.DropColumn(
                name: "RatingSnapshot",
                table: "AiMealPlanItem");

            migrationBuilder.DropColumn(
                name: "ReviewCountSnapshot",
                table: "AiMealPlanItem");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "AiMealPlan");

            migrationBuilder.DropColumn(
                name: "WarningsJson",
                table: "AiMealPlan");
        }
    }
}
