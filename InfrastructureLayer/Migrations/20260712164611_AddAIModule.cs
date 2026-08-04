using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddAIModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIRecommendationLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    NightMarketId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecommendationType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InputJson = table.Column<string>(type: "jsonb", nullable: false),
                    ParsedIntentJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    SelectedOptionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                comment: "Log tối giản cho các lần AI recommendation để debug/demo");

            migrationBuilder.CreateTable(
                name: "FoodTag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TagGroup = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Active'::character varying"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FoodTag_pkey", x => x.Id);
                },
                comment: "Danh sách tag chuẩn mô tả ngữ nghĩa món ăn cho AI/recommendation");

            migrationBuilder.CreateTable(
                name: "CustomerPreference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferenceKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreferenceSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                comment: "Sở thích rõ ràng của khách hàng theo FoodTag: Like/Avoid");

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
                comment: "Bảng nối gắn tag ngữ nghĩa vào món ăn");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIRecommendationLog");

            migrationBuilder.DropTable(
                name: "CustomerPreference");

            migrationBuilder.DropTable(
                name: "FoodItemTag");

            migrationBuilder.DropTable(
                name: "FoodTag");
        }
    }
}
