using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

public partial class AddSystemFoodTaxonomy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FoodCategories_BoothId_fkey", "FoodCategories");

        migrationBuilder.AlterColumn<Guid>(
            name: "BoothId", table: "FoodCategories", type: "uuid", nullable: true,
            oldClrType: typeof(Guid), oldType: "uuid");

        migrationBuilder.AddColumn<string>(
            name: "Code", table: "FoodCategories", type: "character varying(100)",
            maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "DisplayOrder", table: "FoodCategories", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>(
            name: "IsActive", table: "FoodCategories", type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(
            name: "IsSelectable", table: "FoodCategories", type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(
            name: "IsSystem", table: "FoodCategories", type: "boolean", nullable: false, defaultValue: false);

        migrationBuilder.Sql(
            """
            UPDATE "FoodCategories"
            SET "Code" = 'LEGACY_' || upper(replace("Id"::text, '-', ''))
            WHERE "Code" IS NULL OR btrim("Code") = '';
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Code", table: "FoodCategories", type: "character varying(100)",
            maxLength: 100, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(100)", oldMaxLength: 100, oldNullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DisplayOrder", table: "FoodTag", type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<bool>(
            name: "IsAutoAssigned", table: "FoodTag", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(
            name: "IsPreferenceSelectable", table: "FoodTag", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(
            name: "IsSelectable", table: "FoodTag", type: "boolean", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<bool>(
            name: "IsSystem", table: "FoodTag", type: "boolean", nullable: false, defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "ux_foodcategory_code_active", table: "FoodCategories", column: "Code",
            unique: true, filter: "\"IsDeleted\" = false");

        migrationBuilder.AddForeignKey(
            name: "FoodCategories_BoothId_fkey", table: "FoodCategories", column: "BoothId",
            principalTable: "Booth", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM "FoodCategories" c
                    WHERE c."IsSystem" = true
                      AND EXISTS (SELECT 1 FROM "FoodItem" f WHERE f."CategoryId" = c."Id")
                ) THEN
                    RAISE EXCEPTION 'Cannot roll back system taxonomy while food items reference system categories.';
                END IF;
            END $$;
            DELETE FROM "FoodCategories" WHERE "IsSystem" = true;
            """);

        migrationBuilder.DropForeignKey("FoodCategories_BoothId_fkey", "FoodCategories");
        migrationBuilder.DropIndex("ux_foodcategory_code_active", "FoodCategories");
        migrationBuilder.DropColumn("Code", "FoodCategories");
        migrationBuilder.DropColumn("DisplayOrder", "FoodCategories");
        migrationBuilder.DropColumn("IsActive", "FoodCategories");
        migrationBuilder.DropColumn("IsSelectable", "FoodCategories");
        migrationBuilder.DropColumn("IsSystem", "FoodCategories");
        migrationBuilder.DropColumn("DisplayOrder", "FoodTag");
        migrationBuilder.DropColumn("IsAutoAssigned", "FoodTag");
        migrationBuilder.DropColumn("IsPreferenceSelectable", "FoodTag");
        migrationBuilder.DropColumn("IsSelectable", "FoodTag");
        migrationBuilder.DropColumn("IsSystem", "FoodTag");

        migrationBuilder.AlterColumn<Guid>(
            name: "BoothId", table: "FoodCategories", type: "uuid", nullable: false,
            oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AddForeignKey(
            name: "FoodCategories_BoothId_fkey", table: "FoodCategories", column: "BoothId",
            principalTable: "Booth", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
    }
}
