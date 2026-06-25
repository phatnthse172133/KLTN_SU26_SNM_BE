using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddFoodCategorySoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "FoodCategories_Name_key",
                table: "FoodCategories");

            migrationBuilder.AddColumn<Guid>(
                name: "BoothId",
                table: "FoodCategories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "FoodCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "idx_foodcategory_booth",
                table: "FoodCategories",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "FoodCategories_BoothId_Name_key",
                table: "FoodCategories",
                columns: new[] { "BoothId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FoodCategories_BoothId_fkey",
                table: "FoodCategories",
                column: "BoothId",
                principalTable: "Booth",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FoodCategories_BoothId_fkey",
                table: "FoodCategories");

            migrationBuilder.DropIndex(
                name: "FoodCategories_BoothId_Name_key",
                table: "FoodCategories");

            migrationBuilder.DropIndex(
                name: "idx_foodcategory_booth",
                table: "FoodCategories");

            migrationBuilder.DropColumn(
                name: "BoothId",
                table: "FoodCategories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "FoodCategories");

            migrationBuilder.CreateIndex(
                name: "FoodCategories_Name_key",
                table: "FoodCategories",
                column: "Name",
                unique: true);
        }
    }
}
