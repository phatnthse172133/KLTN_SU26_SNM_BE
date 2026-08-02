using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddAiV2NormalizedFoodAndPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimatedServingCount",
                table: "FoodItem",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsShareable",
                table: "FoodItem",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SemanticProfileUpdatedAt",
                table: "FoodItem",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SemanticProfileVersion",
                table: "FoodItem",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ServingSizeDescription",
                table: "FoodItem",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServingTemperature",
                table: "FoodItem",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpiceLevel",
                table: "FoodItem",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "UNKNOWN");

            migrationBuilder.CreateTable(
                name: "AiMealPlanSession",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    Budget = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DiningStyle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalRequest = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ParsedPreferenceJson = table.Column<string>(type: "jsonb", nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    MaxDistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMealPlanSession", x => x.Id);
                    table.CheckConstraint("ck_aimealplansession_budget", "\"Budget\" > 0");
                    table.CheckConstraint("ck_aimealplansession_party", "\"PartySize\" > 0");
                    table.ForeignKey(
                        name: "FK_AiMealPlanSession_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AiRecommendationSession",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalQuery = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ParsedPreferenceJson = table.Column<string>(type: "jsonb", nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    MaxDistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UsedFallback = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRecommendationSession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRecommendationSession_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Allergen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Allergen_pkey", x => x.Id);
                    table.CheckConstraint("ck_allergen_code_normalized", "\"Code\" = upper(btrim(\"Code\"))");
                });

            migrationBuilder.CreateTable(
                name: "CustomerFoodProfile",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferredSpiceLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PreferredPriceMin = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    PreferredPriceMax = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    DefaultMaxDistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                name: "DietaryAttribute",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("DietaryAttribute_pkey", x => x.Id);
                    table.CheckConstraint("ck_dietaryattribute_code_normalized", "\"Code\" = upper(btrim(\"Code\"))");
                });

            migrationBuilder.CreateTable(
                name: "FoodAiProfile",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EmbeddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodAiProfile", x => x.FoodItemId);
                    table.ForeignKey(
                        name: "FK_FoodAiProfile_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodItemCourse",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Course = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItemCourse", x => new { x.FoodItemId, x.Course });
                    table.ForeignKey(
                        name: "FK_FoodItemCourse_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodItemDiningPurpose",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItemDiningPurpose", x => new { x.FoodItemId, x.Purpose });
                    table.ForeignKey(
                        name: "FK_FoodItemDiningPurpose_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodSearchFacet",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FoodSearchFacet_pkey", x => x.Id);
                    table.CheckConstraint("ck_foodsearchfacet_code_normalized", "\"Code\" = upper(btrim(\"Code\"))");
                });

            migrationBuilder.CreateTable(
                name: "Ingredient",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Ingredient_pkey", x => x.Id);
                    table.CheckConstraint("ck_ingredient_code_normalized", "\"Code\" = upper(btrim(\"Code\"))");
                });

            migrationBuilder.CreateTable(
                name: "PreparationMethod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PreparationMethod_pkey", x => x.Id);
                    table.CheckConstraint("ck_preparationmethod_code_normalized", "\"Code\" = upper(btrim(\"Code\"))");
                });

            migrationBuilder.CreateTable(
                name: "TasteProfile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("TasteProfile_pkey", x => x.Id);
                    table.CheckConstraint("ck_tasteprofile_code_normalized", "\"Code\" = upper(btrim(\"Code\"))");
                });

            migrationBuilder.CreateTable(
                name: "AiMealPlan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlanTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strategy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TotalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RemainingBudget = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    EstimatedTravelMinutes = table.Column<int>(type: "integer", nullable: true),
                    EstimatedServingCount = table.Column<int>(type: "integer", nullable: true),
                    CompatibilityScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMealPlan", x => x.Id);
                    table.CheckConstraint("ck_aimealplan_prices", "\"TotalPrice\" >= 0");
                    table.CheckConstraint("ck_aimealplan_score", "\"CompatibilityScore\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_AiMealPlan_AiMealPlanSession_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AiMealPlanSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiMealPlan_NightMarket_MarketId",
                        column: x => x.MarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiRecommendationFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRecommendationFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiRecommendationFeedback_AiRecommendationSession_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AiRecommendationSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiRecommendationFeedback_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FoodItemAllergen",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllergenId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeclarationType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItemAllergen", x => new { x.FoodItemId, x.AllergenId });
                    table.ForeignKey(
                        name: "FK_FoodItemAllergen_Allergen_AllergenId",
                        column: x => x.AllergenId,
                        principalTable: "Allergen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoodItemAllergen_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
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
                name: "FoodItemDietaryAttribute",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DietaryAttributeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuitabilityStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItemDietaryAttribute", x => new { x.FoodItemId, x.DietaryAttributeId });
                    table.ForeignKey(
                        name: "FK_FoodItemDietaryAttribute_DietaryAttribute_DietaryAttributeId",
                        column: x => x.DietaryAttributeId,
                        principalTable: "DietaryAttribute",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FoodItemDietaryAttribute_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FoodItemSearchFacet",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodSearchFacetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItemSearchFacet", x => new { x.FoodItemId, x.FoodSearchFacetId });
                    table.ForeignKey(
                        name: "FK_FoodItemSearchFacet_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FoodItemSearchFacet_FoodSearchFacet_FoodSearchFacetId",
                        column: x => x.FoodSearchFacetId,
                        principalTable: "FoodSearchFacet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "FoodItemIngredient",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItemIngredient", x => new { x.FoodItemId, x.IngredientId });
                    table.ForeignKey(
                        name: "FK_FoodItemIngredient_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FoodItemIngredient_Ingredient_IngredientId",
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
                name: "FoodItemPreparationMethod",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparationMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItemPreparationMethod", x => new { x.FoodItemId, x.PreparationMethodId });
                    table.ForeignKey(
                        name: "FK_FoodItemPreparationMethod_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FoodItemPreparationMethod_PreparationMethod_PreparationMeth~",
                        column: x => x.PreparationMethodId,
                        principalTable: "PreparationMethod",
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

            migrationBuilder.CreateTable(
                name: "FoodItemTasteProfile",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TasteProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Intensity = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodItemTasteProfile", x => new { x.FoodItemId, x.TasteProfileId });
                    table.CheckConstraint("ck_fooditemtaste_intensity", "\"Intensity\" IS NULL OR (\"Intensity\" BETWEEN 1 AND 5)");
                    table.ForeignKey(
                        name: "FK_FoodItemTasteProfile_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FoodItemTasteProfile_TasteProfile_TasteProfileId",
                        column: x => x.TasteProfileId,
                        principalTable: "TasteProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiMealPlanItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: true),
                    FoodNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BoothNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Course = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TotalPriceSnapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ServingCountSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CompatibilityScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMealPlanItem", x => x.Id);
                    table.CheckConstraint("ck_aimealplanitem_prices", "\"UnitPriceSnapshot\" >= 0 AND \"TotalPriceSnapshot\" >= 0");
                    table.CheckConstraint("ck_aimealplanitem_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("ck_aimealplanitem_score", "\"CompatibilityScore\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_AiMealPlanItem_AiMealPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "AiMealPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiMealPlanItem_Booth_BoothId",
                        column: x => x.BoothId,
                        principalTable: "Booth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AiMealPlanItem_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_fooditem_estimated_serving_count",
                table: "FoodItem",
                sql: "\"EstimatedServingCount\" IS NULL OR \"EstimatedServingCount\" > 0");

            migrationBuilder.CreateIndex(
                name: "idx_aimealplan_market",
                table: "AiMealPlan",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "ux_aimealplan_session_code",
                table: "AiMealPlan",
                columns: new[] { "SessionId", "PlanCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_aimealplanitem_plan",
                table: "AiMealPlanItem",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_AiMealPlanItem_BoothId",
                table: "AiMealPlanItem",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_AiMealPlanItem_FoodItemId",
                table: "AiMealPlanItem",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "ux_aimealplanitem_active_food",
                table: "AiMealPlanItem",
                columns: new[] { "PlanId", "FoodItemId" },
                unique: true,
                filter: "\"IsRemoved\" = false AND \"FoodItemId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_aimealplansession_customer_created",
                table: "AiMealPlanSession",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "idx_airecommendationfeedback_session",
                table: "AiRecommendationFeedback",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendationFeedback_FoodItemId",
                table: "AiRecommendationFeedback",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "idx_airecommendationsession_customer_created",
                table: "AiRecommendationSession",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_allergen_code",
                table: "Allergen",
                column: "Code",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ux_dietaryattribute_code",
                table: "DietaryAttribute",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_foodaiprofile_contenthash",
                table: "FoodAiProfile",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_FoodItemAllergen_AllergenId",
                table: "FoodItemAllergen",
                column: "AllergenId");

            migrationBuilder.CreateIndex(
                name: "ux_fooditemcourse_primary",
                table: "FoodItemCourse",
                column: "FoodItemId",
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_FoodItemDietaryAttribute_DietaryAttributeId",
                table: "FoodItemDietaryAttribute",
                column: "DietaryAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodItemIngredient_IngredientId",
                table: "FoodItemIngredient",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodItemPreparationMethod_PreparationMethodId",
                table: "FoodItemPreparationMethod",
                column: "PreparationMethodId");

            migrationBuilder.CreateIndex(
                name: "ux_fooditempreparation_primary",
                table: "FoodItemPreparationMethod",
                column: "FoodItemId",
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_FoodItemSearchFacet_FoodSearchFacetId",
                table: "FoodItemSearchFacet",
                column: "FoodSearchFacetId");

            migrationBuilder.CreateIndex(
                name: "IX_FoodItemTasteProfile_TasteProfileId",
                table: "FoodItemTasteProfile",
                column: "TasteProfileId");

            migrationBuilder.CreateIndex(
                name: "ux_foodsearchfacet_code",
                table: "FoodSearchFacet",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_ingredient_normalized_name",
                table: "Ingredient",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "ux_ingredient_code",
                table: "Ingredient",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_preparationmethod_code",
                table: "PreparationMethod",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tasteprofile_code",
                table: "TasteProfile",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiMealPlanItem");

            migrationBuilder.DropTable(
                name: "AiRecommendationFeedback");

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
                name: "FoodAiProfile");

            migrationBuilder.DropTable(
                name: "FoodItemAllergen");

            migrationBuilder.DropTable(
                name: "FoodItemCourse");

            migrationBuilder.DropTable(
                name: "FoodItemDietaryAttribute");

            migrationBuilder.DropTable(
                name: "FoodItemDiningPurpose");

            migrationBuilder.DropTable(
                name: "FoodItemIngredient");

            migrationBuilder.DropTable(
                name: "FoodItemPreparationMethod");

            migrationBuilder.DropTable(
                name: "FoodItemSearchFacet");

            migrationBuilder.DropTable(
                name: "FoodItemTasteProfile");

            migrationBuilder.DropTable(
                name: "AiMealPlan");

            migrationBuilder.DropTable(
                name: "AiRecommendationSession");

            migrationBuilder.DropTable(
                name: "CustomerFoodProfile");

            migrationBuilder.DropTable(
                name: "Allergen");

            migrationBuilder.DropTable(
                name: "DietaryAttribute");

            migrationBuilder.DropTable(
                name: "Ingredient");

            migrationBuilder.DropTable(
                name: "PreparationMethod");

            migrationBuilder.DropTable(
                name: "FoodSearchFacet");

            migrationBuilder.DropTable(
                name: "TasteProfile");

            migrationBuilder.DropTable(
                name: "AiMealPlanSession");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fooditem_estimated_serving_count",
                table: "FoodItem");

            migrationBuilder.DropColumn(
                name: "EstimatedServingCount",
                table: "FoodItem");

            migrationBuilder.DropColumn(
                name: "IsShareable",
                table: "FoodItem");

            migrationBuilder.DropColumn(
                name: "SemanticProfileUpdatedAt",
                table: "FoodItem");

            migrationBuilder.DropColumn(
                name: "SemanticProfileVersion",
                table: "FoodItem");

            migrationBuilder.DropColumn(
                name: "ServingSizeDescription",
                table: "FoodItem");

            migrationBuilder.DropColumn(
                name: "ServingTemperature",
                table: "FoodItem");

            migrationBuilder.DropColumn(
                name: "SpiceLevel",
                table: "FoodItem");
        }
    }
}
