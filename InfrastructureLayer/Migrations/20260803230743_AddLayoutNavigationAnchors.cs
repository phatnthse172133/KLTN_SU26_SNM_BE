using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddLayoutNavigationAnchors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LayoutNavigationAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    LayoutId = table.Column<Guid>(type: "uuid", nullable: false),
                    LayoutNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnchorType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AnchorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: false),
                    IsCustomerAccessible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    OpeningTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ClosingTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("LayoutNavigationAnchors_pkey", x => x.Id);
                    table.CheckConstraint("ck_navigationanchor_hours_pair", "(\"OpeningTime\" IS NULL) = (\"ClosingTime\" IS NULL)");
                    table.CheckConstraint("ck_navigationanchor_latitude", "\"Latitude\" >= -90 AND \"Latitude\" <= 90");
                    table.CheckConstraint("ck_navigationanchor_longitude", "\"Longitude\" >= -180 AND \"Longitude\" <= 180");
                    table.ForeignKey(
                        name: "LayoutNavigationAnchors_LayoutId_fkey",
                        column: x => x.LayoutId,
                        principalTable: "MarketLayouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "LayoutNavigationAnchors_LayoutNodeId_fkey",
                        column: x => x.LayoutNodeId,
                        principalTable: "LayoutNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LayoutNavigationAnchors_LayoutNodeId",
                table: "LayoutNavigationAnchors",
                column: "LayoutNodeId");

            migrationBuilder.CreateIndex(
                name: "ux_navigationanchor_layout_code",
                table: "LayoutNavigationAnchors",
                columns: new[] { "LayoutId", "AnchorCode" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LayoutNavigationAnchors");
        }
    }
}
