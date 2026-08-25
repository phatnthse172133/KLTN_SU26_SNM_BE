using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageAttachmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Message",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Text'::character varying",
                comment: "Text | Image | System | File",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Text'::character varying",
                oldComment: "Text | Image | System");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentMimeType",
                table: "Message",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentName",
                table: "Message",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttachmentSize",
                table: "Message",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "Message",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentMimeType",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "AttachmentName",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "AttachmentSize",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "Message");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Message",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Text'::character varying",
                comment: "Text | Image | System",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Text'::character varying",
                oldComment: "Text | Image | System | File");
        }
    }
}
