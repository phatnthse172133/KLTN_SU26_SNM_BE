using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260804033000_ReplaceBrokenCuratedImageLinks")]
public sealed class ReplaceBrokenCuratedImageLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            WITH replacements("FoodId", "ImageUrl") AS (VALUES
              ('a2650000-0000-0000-0000-000000000023'::uuid, 'https://upload.wikimedia.org/wikipedia/commons/6/6d/Prawn_fried_rice_shrimp_bowls.jpg'),
              ('a2650000-0000-0000-0000-000000000029'::uuid, 'https://upload.wikimedia.org/wikipedia/commons/4/44/Yangzhou_fried_rice_and_drinks_06-09-2019.jpg'),
              ('a2650000-0000-0000-0000-000000000030'::uuid, 'https://upload.wikimedia.org/wikipedia/commons/f/f8/Vietnamese_thit_kho%2C_braised_pork_belly_and_boiled_eggs.jpg')
            )
            UPDATE "FoodItem" f SET "ThumbnailUrl" = r."ImageUrl", "UpdatedAt" = now()
            FROM replacements r WHERE f."Id" = r."FoodId";

            WITH replacements("FoodId", "ImageUrl") AS (VALUES
              ('a2650000-0000-0000-0000-000000000023'::uuid, 'https://upload.wikimedia.org/wikipedia/commons/6/6d/Prawn_fried_rice_shrimp_bowls.jpg'),
              ('a2650000-0000-0000-0000-000000000029'::uuid, 'https://upload.wikimedia.org/wikipedia/commons/4/44/Yangzhou_fried_rice_and_drinks_06-09-2019.jpg'),
              ('a2650000-0000-0000-0000-000000000030'::uuid, 'https://upload.wikimedia.org/wikipedia/commons/f/f8/Vietnamese_thit_kho%2C_braised_pork_belly_and_boiled_eggs.jpg')
            )
            UPDATE "FoodImages" i SET "ImageUrl" = r."ImageUrl", "UpdatedAt" = now()
            FROM replacements r WHERE i."FoodItemId" = r."FoodId";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
