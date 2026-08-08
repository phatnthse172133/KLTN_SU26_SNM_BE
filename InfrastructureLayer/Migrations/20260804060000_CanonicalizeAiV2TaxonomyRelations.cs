using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260804060000_CanonicalizeAiV2TaxonomyRelations")]
public sealed class CanonicalizeAiV2TaxonomyRelations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "FoodItemIngredient" r SET "IsPrimary" = false
            FROM "Ingredient" legacy
            WHERE legacy."Id" = r."IngredientId" AND legacy."Code" NOT LIKE 'ING\_%' ESCAPE '\'
              AND EXISTS (SELECT 1 FROM "FoodItemIngredient" other WHERE other."FoodItemId" = r."FoodItemId"
                AND other."IngredientId" <> r."IngredientId" AND other."IsPrimary");
            DELETE FROM "FoodItemIngredient" r USING "Ingredient" legacy, "Ingredient" canonical
            WHERE legacy."Id" = r."IngredientId" AND canonical."Code" = 'ING_' || legacy."Code"
              AND EXISTS (SELECT 1 FROM "FoodItemIngredient" x WHERE x."FoodItemId" = r."FoodItemId" AND x."IngredientId" = canonical."Id");
            UPDATE "FoodItemIngredient" r SET "IngredientId" = canonical."Id"
            FROM "Ingredient" legacy, "Ingredient" canonical
            WHERE legacy."Id" = r."IngredientId" AND canonical."Code" = 'ING_' || legacy."Code";

            UPDATE "FoodItemPreparationMethod" r SET "IsPrimary" = false
            FROM "PreparationMethod" legacy
            WHERE legacy."Id" = r."PreparationMethodId" AND legacy."Code" NOT LIKE 'METHOD\_%' ESCAPE '\'
              AND EXISTS (SELECT 1 FROM "FoodItemPreparationMethod" other WHERE other."FoodItemId" = r."FoodItemId"
                AND other."PreparationMethodId" <> r."PreparationMethodId" AND other."IsPrimary");
            DELETE FROM "FoodItemPreparationMethod" r USING "PreparationMethod" legacy, "PreparationMethod" canonical
            WHERE legacy."Id" = r."PreparationMethodId" AND canonical."Code" = 'METHOD_' || legacy."Code"
              AND EXISTS (SELECT 1 FROM "FoodItemPreparationMethod" x WHERE x."FoodItemId" = r."FoodItemId" AND x."PreparationMethodId" = canonical."Id");
            UPDATE "FoodItemPreparationMethod" r SET "PreparationMethodId" = canonical."Id"
            FROM "PreparationMethod" legacy, "PreparationMethod" canonical
            WHERE legacy."Id" = r."PreparationMethodId" AND canonical."Code" = 'METHOD_' || legacy."Code";

            DELETE FROM "FoodItemTasteProfile" r USING "TasteProfile" legacy, "TasteProfile" canonical
            WHERE legacy."Id" = r."TasteProfileId" AND canonical."Code" = 'TASTE_' || legacy."Code"
              AND EXISTS (SELECT 1 FROM "FoodItemTasteProfile" x WHERE x."FoodItemId" = r."FoodItemId" AND x."TasteProfileId" = canonical."Id");
            UPDATE "FoodItemTasteProfile" r SET "TasteProfileId" = canonical."Id"
            FROM "TasteProfile" legacy, "TasteProfile" canonical
            WHERE legacy."Id" = r."TasteProfileId" AND canonical."Code" = 'TASTE_' || legacy."Code";

            DELETE FROM "FoodItemDietaryAttribute" r USING "DietaryAttribute" legacy, "DietaryAttribute" canonical
            WHERE legacy."Id" = r."DietaryAttributeId" AND canonical."Code" = 'DIET_' || legacy."Code"
              AND EXISTS (SELECT 1 FROM "FoodItemDietaryAttribute" x WHERE x."FoodItemId" = r."FoodItemId" AND x."DietaryAttributeId" = canonical."Id");
            UPDATE "FoodItemDietaryAttribute" r SET "DietaryAttributeId" = canonical."Id"
            FROM "DietaryAttribute" legacy, "DietaryAttribute" canonical
            WHERE legacy."Id" = r."DietaryAttributeId" AND canonical."Code" = 'DIET_' || legacy."Code";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Canonical relation codes are intentionally retained. Reintroducing
        // duplicate legacy taxonomy relations would make matching ambiguous.
    }
}
