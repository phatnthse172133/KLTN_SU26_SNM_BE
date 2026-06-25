using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintResolutionFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PolicyViolation",
                table: "Complaints",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionAction",
                table: "Complaints",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                comment: "NoViolation | Warning | SuspendBooth | CloseBooth");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PolicyViolation",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "ResolutionAction",
                table: "Complaints");
        }
    }
}
