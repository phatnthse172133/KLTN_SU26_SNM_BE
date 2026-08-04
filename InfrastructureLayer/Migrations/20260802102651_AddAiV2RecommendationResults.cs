using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddAiV2RecommendationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderFailureCategory",
                table: "AiRecommendationSession",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderModelName",
                table: "AiRecommendationSession",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRequestId",
                table: "AiRecommendationSession",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiRecommendationResult",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MatchTier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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

            migrationBuilder.CreateIndex(
                name: "ux_airecommendationfeedback_state",
                table: "AiRecommendationFeedback",
                columns: new[] { "SessionId", "FoodItemId" },
                unique: true,
                filter: "\"FoodItemId\" IS NOT NULL AND \"Action\" IN ('LIKED', 'DISLIKED')");

            migrationBuilder.CreateIndex(
                name: "IX_AiRecommendationResult_FoodItemId",
                table: "AiRecommendationResult",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "ux_airecommendationresult_session_rank",
                table: "AiRecommendationResult",
                columns: new[] { "SessionId", "Rank" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiRecommendationResult");

            migrationBuilder.DropIndex(
                name: "ux_airecommendationfeedback_state",
                table: "AiRecommendationFeedback");

            migrationBuilder.DropColumn(
                name: "ProviderFailureCategory",
                table: "AiRecommendationSession");

            migrationBuilder.DropColumn(
                name: "ProviderModelName",
                table: "AiRecommendationSession");

            migrationBuilder.DropColumn(
                name: "ProviderRequestId",
                table: "AiRecommendationSession");
        }
    }
}
