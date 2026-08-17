using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCustomerFoodProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAllergenExclusion");

            migrationBuilder.DropTable(
                name: "CustomerAvoidedIngredient");

            migrationBuilder.DropTable(
                name: "CustomerAvoidedTasteProfile");

            migrationBuilder.DropTable(
                name: "CustomerDietaryRequirement");

            migrationBuilder.DropTable(
                name: "CustomerPreferredCourse");

            migrationBuilder.DropTable(
                name: "CustomerPreferredDiningPurpose");

            migrationBuilder.DropTable(
                name: "CustomerPreferredIngredient");

            migrationBuilder.DropTable(
                name: "CustomerPreferredPreparationMethod");

            migrationBuilder.DropTable(
                name: "CustomerPreferredTasteProfile");

            migrationBuilder.DropTable(
                name: "CustomerFoodProfile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerFoodProfile",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DefaultMaxDistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    PreferredPriceMax = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    PreferredPriceMin = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    PreferredSpiceLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerFoodProfile", x => x.CustomerId);
                    table.CheckConstraint("ck_customerfoodprofile_distance", "\"DefaultMaxDistanceMeters\" IS NULL OR \"DefaultMaxDistanceMeters\" > 0");
                    table.CheckConstraint("ck_customerfoodprofile_price_range", "\"PreferredPriceMin\" IS NULL OR \"PreferredPriceMax\" IS NULL OR \"PreferredPriceMin\" <= \"PreferredPriceMax\"");
                    table.ForeignKey(
                        name: "FK_CustomerFoodProfile_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAllergenExclusion",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllergenId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAllergenExclusion", x => new { x.CustomerId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_CustomerAllergenExclusion_Allergen_AllergenId",
                        column: x => x.AllergenId,
                        principalTable: "Allergen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerAllergenExclusion_CustomerFoodProfile_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerFoodProfile",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAvoidedIngredient",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAvoidedIngredient", x => new { x.CustomerId, x.IngredientId });
                    table.ForeignKey(
                        name: "FK_CustomerAvoidedIngredient_CustomerFoodProfile_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerFoodProfile",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerAvoidedIngredient_Ingredient_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAvoidedTasteProfile",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TasteProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAvoidedTasteProfile", x => new { x.CustomerId, x.TasteProfileId });
                    table.ForeignKey(
                        name: "FK_CustomerAvoidedTasteProfile_CustomerFoodProfile_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerFoodProfile",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerAvoidedTasteProfile_TasteProfile_TasteProfileId",
                        column: x => x.TasteProfileId,
                        principalTable: "TasteProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerDietaryRequirement",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietaryAttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDietaryRequirement", x => new { x.CustomerId, x.DietaryAttributeId });
                    table.ForeignKey(
                        name: "FK_CustomerDietaryRequirement_CustomerFoodProfile_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerFoodProfile",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerDietaryRequirement_DietaryAttribute_DietaryAttribut~",
                        column: x => x.DietaryAttributeId,
                        principalTable: "DietaryAttribute",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPreferredCourse",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Course = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPreferredCourse", x => new { x.CustomerId, x.Course });
                    table.ForeignKey(
                        name: "FK_CustomerPreferredCourse_CustomerFoodProfile_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerFoodProfile",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPreferredDiningPurpose",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPreferredDiningPurpose", x => new { x.CustomerId, x.Purpose });
                    table.ForeignKey(
                        name: "FK_CustomerPreferredDiningPurpose_CustomerFoodProfile_Customer~",
                        column: x => x.CustomerId,
                        principalTable: "CustomerFoodProfile",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPreferredIngredient",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPreferredIngredient", x => new { x.CustomerId, x.IngredientId });
                    table.ForeignKey(
                        name: "FK_CustomerPreferredIngredient_CustomerFoodProfile_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerFoodProfile",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerPreferredIngredient_Ingredient_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPreferredPreparationMethod",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparationMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPreferredPreparationMethod", x => new { x.CustomerId, x.PreparationMethodId });
                    table.ForeignKey(
                        name: "FK_CustomerPreferredPreparationMethod_CustomerFoodProfile_Cust~",
                        column: x => x.CustomerId,
                        principalTable: "CustomerFoodProfile",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerPreferredPreparationMethod_PreparationMethod_Prepar~",
                        column: x => x.PreparationMethodId,
                        principalTable: "PreparationMethod",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPreferredTasteProfile",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TasteProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPreferredTasteProfile", x => new { x.CustomerId, x.TasteProfileId });
                    table.ForeignKey(
                        name: "FK_CustomerPreferredTasteProfile_CustomerFoodProfile_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerFoodProfile",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomerPreferredTasteProfile_TasteProfile_TasteProfileId",
                        column: x => x.TasteProfileId,
                        principalTable: "TasteProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAllergenExclusion_AllergenId",
                table: "CustomerAllergenExclusion",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAvoidedIngredient_IngredientId",
                table: "CustomerAvoidedIngredient",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAvoidedTasteProfile_TasteProfileId",
                table: "CustomerAvoidedTasteProfile",
                column: "TasteProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDietaryRequirement_DietaryAttributeId",
                table: "CustomerDietaryRequirement",
                column: "DietaryAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferredIngredient_IngredientId",
                table: "CustomerPreferredIngredient",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferredPreparationMethod_PreparationMethodId",
                table: "CustomerPreferredPreparationMethod",
                column: "PreparationMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPreferredTasteProfile_TasteProfileId",
                table: "CustomerPreferredTasteProfile",
                column: "TasteProfileId");
        }
    }
}
