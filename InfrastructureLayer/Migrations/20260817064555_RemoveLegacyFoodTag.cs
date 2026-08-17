using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyFoodTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FoodItemTag");

            migrationBuilder.DropTable(
                name: "FoodTag");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FoodTag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsAutoAssigned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsPreferenceSelectable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsSelectable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Active'::character varying"),
                    TagGroup = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FoodTag_pkey", x => x.Id);
                },
                comment: "Danh sách tag chuẩn mô tả ngữ nghĩa món ăn");

            migrationBuilder.CreateTable(
                name: "FoodItemTag",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FoodItemTag_pkey", x => new { x.FoodItemId, x.FoodTagId });
                    table.ForeignKey(
                        name: "FoodItemTag_FoodItemId_fkey",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FoodItemTag_FoodTagId_fkey",
                        column: x => x.FoodTagId,
                        principalTable: "FoodTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "BÃ¡ÂºÂ£ng nÃ¡Â»â€˜i gÃ¡ÂºÂ¯n tag ngÃ¡Â»Â¯ nghÃ„Â©a vÃƒÂ o mÃƒÂ³n Ã„Æ’n");

            migrationBuilder.CreateIndex(
                name: "idx_fooditemtag_foodtag",
                table: "FoodItemTag",
                column: "FoodTagId");

            migrationBuilder.CreateIndex(
                name: "ux_foodtag_code_active",
                table: "FoodTag",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ux_foodtag_name_active",
                table: "FoodTag",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
