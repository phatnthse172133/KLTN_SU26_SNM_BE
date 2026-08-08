using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260804030000_CorrectCuratedMenuIngredientMetadata")]
public sealed class CorrectCuratedMenuIngredientMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO "Ingredient" ("Id", "Code", "Name", "NormalizedName", "IsSystem", "IsActive", "DisplayOrder", "CreatedAt", "UpdatedAt") VALUES
              ('a2670000-0000-0000-0000-000000000009','FISH','Cá','ca',true,true,9,now(),now()),
              ('a2670000-0000-0000-0000-000000000010','POTATO','Khoai','khoai',true,true,10,now(),now()),
              ('a2670000-0000-0000-0000-000000000011','CHEESE','Phô mai','pho mai',true,true,11,now(),now()),
              ('a2670000-0000-0000-0000-000000000012','CORN','Bắp','bap',true,true,12,now(),now()),
              ('a2670000-0000-0000-0000-000000000013','EGG','Trứng','trung',true,true,13,now(),now()),
              ('a2670000-0000-0000-0000-000000000014','TOFU','Đậu hũ','dau hu',true,true,14,now(),now())
            ON CONFLICT ("Code") DO NOTHING;

            DELETE FROM "FoodItemIngredient"
            WHERE "FoodItemId"::text LIKE 'a2650000-0000-0000-0000-%';

            INSERT INTO "FoodItemIngredient" ("FoodItemId", "IngredientId", "IsPrimary", "IsOptional", "CreatedAt")
            SELECT f."Id", i."Id", true, false, now()
            FROM "FoodItem" f
            JOIN "Ingredient" i ON i."Code" = CASE
                WHEN lower(f."Name") LIKE '%chay%' THEN 'VEGETABLE'
                WHEN lower(f."Name") ~ '(hải sản|tôm|mực|cua|hàu|nghêu|sò|bạch tuộc|ốc hương|hến)' THEN 'SEAFOOD'
                WHEN lower(f."Name") LIKE '%cá viên%' THEN 'FISH'
                WHEN lower(f."Name") LIKE '%phô mai%' THEN 'CHEESE'
                WHEN lower(f."Name") LIKE '%khoai%' THEN 'POTATO'
                WHEN lower(f."Name") LIKE '%bắp%' THEN 'CORN'
                WHEN lower(f."Name") LIKE '%trứng%' THEN 'EGG'
                WHEN lower(f."Name") LIKE '%đậu hũ%' THEN 'TOFU'
                WHEN lower(f."Name") LIKE '%gà%' THEN 'CHICKEN'
                WHEN lower(f."Name") ~ '(bò|phở)' THEN 'BEEF'
                WHEN lower(f."Name") ~ '(sườn|thịt|ba chỉ|xúc xích|chả giò|bánh bao)' THEN 'PORK'
                WHEN f."BoothId" = 'a2630000-0000-0000-0000-000000000007' THEN 'FRUIT'
                WHEN f."BoothId" = 'a2630000-0000-0000-0000-000000000008' THEN 'COFFEE_TEA'
                WHEN f."BoothId" = 'a2630000-0000-0000-0000-000000000009' THEN 'VEGETABLE'
                ELSE 'RICE_FLOUR'
            END
            WHERE f."Id"::text LIKE 'a2650000-0000-0000-0000-%';

            INSERT INTO "FoodItemDietaryAttribute" ("FoodItemId", "DietaryAttributeId", "SuitabilityStatus", "IsConfirmed", "Source", "CreatedAt", "UpdatedAt")
            SELECT f."Id", d."Id", 'SUITABLE', true, 'ADMIN_VERIFIED', now(), now()
            FROM "FoodItem" f
            CROSS JOIN "DietaryAttribute" d
            WHERE d."Code" = 'VEGETARIAN'
              AND f."Id"::text LIKE 'a2650000-0000-0000-0000-%'
              AND (lower(f."Name") LIKE '%chay%' OR f."BoothId" = 'a2630000-0000-0000-0000-000000000009')
            ON CONFLICT DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The previous migration's broad concept metadata remains safe on rollback.
    }
}
