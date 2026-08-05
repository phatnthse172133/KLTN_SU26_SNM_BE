using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketLayoutGraphRevisionAndDraftClone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BoothLocations_LayoutId",
                table: "BoothLocations");

            migrationBuilder.DropIndex(
                name: "ux_boothlocation_active_booth",
                table: "BoothLocations");

            migrationBuilder.DropIndex(
                name: "ux_boothlocation_active_node",
                table: "BoothLocations");

            migrationBuilder.AddColumn<int>(
                name: "GraphRevision",
                table: "MarketLayouts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_marketlayout_graph_revision_positive",
                table: "MarketLayouts",
                sql: "\"GraphRevision\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_BoothLocations_BoothId",
                table: "BoothLocations",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "IX_BoothLocations_LayoutNodeId",
                table: "BoothLocations",
                column: "LayoutNodeId");

            migrationBuilder.CreateIndex(
                name: "ux_boothlocation_active_layout_booth",
                table: "BoothLocations",
                columns: new[] { "LayoutId", "BoothId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ux_boothlocation_active_layout_node",
                table: "BoothLocations",
                columns: new[] { "LayoutId", "LayoutNodeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_marketlayout_graph_revision_positive",
                table: "MarketLayouts");

            migrationBuilder.DropIndex(
                name: "IX_BoothLocations_BoothId",
                table: "BoothLocations");

            migrationBuilder.DropIndex(
                name: "IX_BoothLocations_LayoutNodeId",
                table: "BoothLocations");

            migrationBuilder.DropIndex(
                name: "ux_boothlocation_active_layout_booth",
                table: "BoothLocations");

            migrationBuilder.DropIndex(
                name: "ux_boothlocation_active_layout_node",
                table: "BoothLocations");

            migrationBuilder.DropColumn(
                name: "GraphRevision",
                table: "MarketLayouts");

            migrationBuilder.CreateIndex(
                name: "IX_BoothLocations_LayoutId",
                table: "BoothLocations",
                column: "LayoutId");

            migrationBuilder.CreateIndex(
                name: "ux_boothlocation_active_booth",
                table: "BoothLocations",
                column: "BoothId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ux_boothlocation_active_node",
                table: "BoothLocations",
                column: "LayoutNodeId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
