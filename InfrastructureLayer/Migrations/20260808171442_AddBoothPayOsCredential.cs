using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddBoothPayOsCredential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Complaints",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Other",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValueSql: "'Other'::character varying");

            migrationBuilder.CreateTable(
                name: "BoothPayOsCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoothId = table.Column<Guid>(type: "uuid", nullable: false),
                    EncryptedClientId = table.Column<string>(type: "text", nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "text", nullable: false),
                    EncryptedChecksumKey = table.Column<string>(type: "text", nullable: false),
                    EncryptedPayoutClientId = table.Column<string>(type: "text", nullable: true),
                    EncryptedPayoutApiKey = table.Column<string>(type: "text", nullable: true),
                    EncryptedPayoutChecksumKey = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoothPayOsCredentials", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoothPayOsCredentials");

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Complaints",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'Other'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "Other");
        }
    }
}
