using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RepairPhase2CinemaLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_layoutnodes_active_layout_slotcode",
                table: "LayoutNodes",
                columns: new[] { "LayoutId", "SlotCode" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"SlotCode\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_layoutnodes_active_layout_slotcode",
                table: "LayoutNodes");
        }
    }
}
