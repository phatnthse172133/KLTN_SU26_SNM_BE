using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSubscriptionStatusDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MarketSubscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "Active | Expired | Cancelled | PendingPayment",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Active'::character varying",
                oldComment: "Active | Expired | Cancelled | PendingPayment");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "BoothSubscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "Active | Expired | Cancelled",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Active'::character varying",
                oldComment: "Active | Expired | Cancelled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "MarketSubscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Active'::character varying",
                comment: "Active | Expired | Cancelled | PendingPayment",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "Active | Expired | Cancelled | PendingPayment");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "BoothSubscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Active'::character varying",
                comment: "Active | Expired | Cancelled",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "Active | Expired | Cancelled");
        }
    }
}
