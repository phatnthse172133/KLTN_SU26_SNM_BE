using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullableUserPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "User",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                comment: "MÃ¡ÂºÂ­t khÃ¡ÂºÂ©u Ã„â€˜ÃƒÂ£ Ã„â€˜Ã†Â°Ã¡Â»Â£c mÃƒÂ£ hÃƒÂ³a (hash), tuyÃ¡Â»â€¡t Ã„â€˜Ã¡Â»â€˜i khÃƒÂ´ng lÃ†Â°u plaintext",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldComment: "MÃ¡ÂºÂ­t khÃ¡ÂºÂ©u Ã„â€˜ÃƒÂ£ Ã„â€˜Ã†Â°Ã¡Â»Â£c mÃƒÂ£ hÃƒÂ³a (hash), tuyÃ¡Â»â€¡t Ã„â€˜Ã¡Â»â€˜i khÃƒÂ´ng lÃ†Â°u plaintext");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "User",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                comment: "MÃ¡ÂºÂ­t khÃ¡ÂºÂ©u Ã„â€˜ÃƒÂ£ Ã„â€˜Ã†Â°Ã¡Â»Â£c mÃƒÂ£ hÃƒÂ³a (hash), tuyÃ¡Â»â€¡t Ã„â€˜Ã¡Â»â€˜i khÃƒÂ´ng lÃ†Â°u plaintext",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true,
                oldComment: "MÃ¡ÂºÂ­t khÃ¡ÂºÂ©u Ã„â€˜ÃƒÂ£ Ã„â€˜Ã†Â°Ã¡Â»Â£c mÃƒÂ£ hÃƒÂ³a (hash), tuyÃ¡Â»â€¡t Ã„â€˜Ã¡Â»â€˜i khÃƒÂ´ng lÃ†Â°u plaintext");
        }
    }
}
