using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddNightMarketImagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NightMarketImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    NightMarketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsCover = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("NightMarketImages_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "NightMarketImages_NightMarketId_fkey",
                        column: x => x.NightMarketId,
                        principalTable: "NightMarket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Thư viện ảnh (gallery) của chợ đêm");

            migrationBuilder.CreateIndex(
                name: "idx_nightmarketimage_market",
                table: "NightMarketImages",
                column: "NightMarketId");

            migrationBuilder.CreateIndex(
                name: "ux_nightmarketimage_one_cover",
                table: "NightMarketImages",
                column: "NightMarketId",
                unique: true,
                filter: "\"IsCover\" = true AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NightMarketImages");
        }
    }
}
