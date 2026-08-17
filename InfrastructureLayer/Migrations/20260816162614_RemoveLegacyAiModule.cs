using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyAiModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiMealPlanCartOperation");

            migrationBuilder.DropTable(
                name: "AiMealPlanItem");

            migrationBuilder.DropTable(
                name: "AiRecommendationFeedback");

            migrationBuilder.DropTable(
                name: "AIRecommendationLog");

            migrationBuilder.DropTable(
                name: "AiRecommendationResult");

            migrationBuilder.DropTable(
                name: "CustomerPreference");

            migrationBuilder.DropTable(
                name: "FoodAiProfile");

            migrationBuilder.DropTable(
                name: "AiMealPlan");

            migrationBuilder.DropTable(
                name: "AiRecommendationSession");

            migrationBuilder.DropTable(
                name: "AiMealPlanSession");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiMealPlanSession",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Budget = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DiningStyle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    MaxDistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    OriginalRequest = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ParsedPreferenceJson = table.Column<string>(type: "jsonb", nullable: true),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UsedProviderFallback = table.Column<bool>(type: "boolean", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: true)
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
                name: "AIRecommendationLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    NightMarketId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    InputJson = table.Column<string>(type: "jsonb", nullable: false),
                    ParsedIntentJson = table.Column<string>(type: "jsonb", nullable: true),
                    RecommendationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    SelectedOptionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("AIRecommendationLog_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "AIRecommendationLog_CustomerId_fkey",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "AIRecommendationLog_NightMarketId_fkey",
                        column: x => x.NightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Log tÃ¡Â»â€˜i giÃ¡ÂºÂ£n cho cÃƒÂ¡c lÃ¡ÂºÂ§n AI recommendation Ã„â€˜Ã¡Â»Æ’ debug/demo");

            migrationBuilder.CreateTable(
                name: "AiRecommendationSession",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    MaxDistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    OriginalQuery = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ParsedPreferenceJson = table.Column<string>(type: "jsonb", nullable: true),
                    ProviderFailureCategory = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProviderModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProviderRequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UsedFallback = table.Column<bool>(type: "boolean", nullable: false)
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
                name: "CustomerPreference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    PreferenceKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreferenceSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("CustomerPreference_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "CustomerPreference_CustomerId_fkey",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "CustomerPreference_FoodTagId_fkey",
                        column: x => x.FoodTagId,
                        principalTable: "FoodTag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                },
                comment: "SÃ¡Â»Å¸ thÃƒÂ­ch rÃƒÂµ rÃƒÂ ng cÃ¡Â»Â§a khÃƒÂ¡ch hÃƒÂ ng theo FoodTag: Like/Avoid");

            migrationBuilder.CreateTable(
                name: "FoodAiProfile",
                columns: table => new
                {
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiDescription = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    EmbeddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GeneratedByModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SearchText = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StructuredProfileJson = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Version = table.Column<int>(type: "integer", nullable: false)
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
                name: "AiMealPlan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    MarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompatibilityScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    DistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    EstimatedServingCount = table.Column<int>(type: "integer", nullable: true),
                    EstimatedTravelMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlanTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RemainingBudget = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Strategy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TotalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: true)
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
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "AiRecommendationResult",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    MatchTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiRecommendationResult", x => new { x.SessionId, x.FoodItemId });
                    table.CheckConstraint("ck_airecommendationresult_score", "\"Score\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_AiRecommendationResult_AiRecommendationSession_SessionId",
                        column: x => x.SessionId,
                        principalTable: "AiRecommendationSession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiRecommendationResult_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiMealPlanCartOperation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanVersion = table.Column<int>(type: "integer", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiMealPlanCartOperation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiMealPlanCartOperation_AiMealPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "AiMealPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiMealPlanCartOperation_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiMealPlanItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: true),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompatibilityScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Course = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    FoodNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImageUrlSnapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RatingSnapshot = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReviewCountSnapshot = table.Column<int>(type: "integer", nullable: false),
                    ServingCountSnapshot = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TotalPriceSnapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
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
                name: "idx_aimealplancart_plan",
                table: "AiMealPlanCartOperation",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "ux_aimealplancart_customer_key",
                table: "AiMealPlanCartOperation",
                columns: new[] { "CustomerId", "IdempotencyKey" },
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
                name: "ux_aimealplansession_customer_idempotency",
                table: "AiMealPlanSession",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true,
                filter: "\"CustomerId\" IS NOT NULL AND \"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_airecommendationfeedback_session",
                table: "AiRecommendationFeedback",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendationFeedback_FoodItemId",
                table: "AiRecommendationFeedback",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "ux_airecommendationfeedback_state",
                table: "AiRecommendationFeedback",
                columns: new[] { "SessionId", "FoodItemId" },
                unique: true,
                filter: "\"FoodItemId\" IS NOT NULL AND \"Action\" IN ('LIKED', 'DISLIKED')");

            migrationBuilder.CreateIndex(
                name: "idx_airecommendationlog_customer",
                table: "AIRecommendationLog",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "idx_airecommendationlog_nightmarket",
                table: "AIRecommendationLog",
                column: "NightMarketId");

            migrationBuilder.CreateIndex(
                name: "idx_airecommendationlog_type_created",
                table: "AIRecommendationLog",
                columns: new[] { "RecommendationType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendationResult_FoodItemId",
                table: "AiRecommendationResult",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "ux_airecommendationresult_session_rank",
                table: "AiRecommendationResult",
                columns: new[] { "SessionId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_airecommendationsession_customer_created",
                table: "AiRecommendationSession",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "idx_customerpreference_customer",
                table: "CustomerPreference",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "idx_customerpreference_foodtag",
                table: "CustomerPreference",
                column: "FoodTagId");

            migrationBuilder.CreateIndex(
                name: "ux_customerpreference_tag_kind",
                table: "CustomerPreference",
                columns: new[] { "CustomerId", "FoodTagId", "PreferenceKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_foodaiprofile_contenthash",
                table: "FoodAiProfile",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "idx_foodaiprofile_status",
                table: "FoodAiProfile",
                column: "Status");
        }
    }
}
