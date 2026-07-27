using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AnchorChatConversationsToBooth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Conversations_BoothOwnerId_fkey",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "uq_conversation_customer_boothowner",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_BoothOwnerId",
                table: "Conversations");

            migrationBuilder.AddColumn<Guid>(
                name: "BoothId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Conversations" AS conversation
                SET "BoothId" = booth."Id"
                FROM "Booth" AS booth
                WHERE booth."BoothOwnerId" = conversation."BoothOwnerId";

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Conversations" WHERE "BoothId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot anchor every existing conversation to a booth.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "BoothId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "BoothOwnerId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_BoothId",
                table: "Conversations",
                column: "BoothId");

            migrationBuilder.CreateIndex(
                name: "uq_conversation_customer_booth",
                table: "Conversations",
                columns: new[] { "CustomerId", "BoothId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "Conversations_BoothId_fkey",
                table: "Conversations",
                column: "BoothId",
                principalTable: "Booth",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Conversations_BoothId_fkey",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "uq_conversation_customer_booth",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_BoothId",
                table: "Conversations");

            migrationBuilder.AddColumn<Guid>(
                name: "BoothOwnerId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Conversations" AS conversation
                SET "BoothOwnerId" = booth."BoothOwnerId"
                FROM "Booth" AS booth
                WHERE booth."Id" = conversation."BoothId";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "BoothOwnerId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "BoothId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_BoothOwnerId",
                table: "Conversations",
                column: "BoothOwnerId");

            migrationBuilder.CreateIndex(
                name: "uq_conversation_customer_boothowner",
                table: "Conversations",
                columns: new[] { "CustomerId", "BoothOwnerId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "Conversations_BoothOwnerId_fkey",
                table: "Conversations",
                column: "BoothOwnerId",
                principalTable: "User",
                principalColumn: "Id");
        }
    }
}
