using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "FoodTag",
                comment: "Danh sách tag chuẩn mô tả ngữ nghĩa món ăn",
                oldComment: "Danh sÃƒÂ¡ch tag chuÃ¡ÂºÂ©n mÃƒÂ´ tÃ¡ÂºÂ£ ngÃ¡Â»Â¯ nghÃ„Â©a mÃƒÂ³n Ã„Æ’n cho AI/recommendation");

            migrationBuilder.CreateTable(
                name: "AssistantConversation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PendingUserMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantConversation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantConversation_NightMarket_MarketId",
                        column: x => x.MarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssistantConversation_User_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantMealPlan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    NightMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    EstimatedTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    BudgetMax = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantMealPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantMealPlan_AssistantConversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AssistantConversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistantMealPlan_NightMarket_NightMarketId",
                        column: x => x.NightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantMessage_AssistantConversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "AssistantConversation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantMealPlanItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    MealPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantMealPlanItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantMealPlanItem_AssistantMealPlan_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "AssistantMealPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistantMealPlanItem_FoodItem_FoodItemId",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_assistantconversation_customer",
                table: "AssistantConversation",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConversation_MarketId",
                table: "AssistantConversation",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "idx_assistantmealplan_conversation",
                table: "AssistantMealPlan",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantMealPlan_NightMarketId",
                table: "AssistantMealPlan",
                column: "NightMarketId");

            migrationBuilder.CreateIndex(
                name: "idx_assistantmealplanitem_plan",
                table: "AssistantMealPlanItem",
                columns: new[] { "MealPlanId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantMealPlanItem_FoodItemId",
                table: "AssistantMealPlanItem",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "idx_assistantmessage_conversation",
                table: "AssistantMessage",
                columns: new[] { "ConversationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantMealPlanItem");

            migrationBuilder.DropTable(
                name: "AssistantMessage");

            migrationBuilder.DropTable(
                name: "AssistantMealPlan");

            migrationBuilder.DropTable(
                name: "AssistantConversation");

            migrationBuilder.AlterTable(
                name: "FoodTag",
                comment: "Danh sÃƒÂ¡ch tag chuÃ¡ÂºÂ©n mÃƒÂ´ tÃ¡ÂºÂ£ ngÃ¡Â»Â¯ nghÃ„Â©a mÃƒÂ³n Ã„Æ’n cho AI/recommendation",
                oldComment: "Danh sách tag chuẩn mô tả ngữ nghĩa món ăn");
        }
    }
}
