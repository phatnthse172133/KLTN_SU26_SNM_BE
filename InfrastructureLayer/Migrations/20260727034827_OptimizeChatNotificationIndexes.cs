using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeChatNotificationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_message_conversation",
                table: "Message");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_BoothId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "idx_notification_user_read",
                table: "Notification",
                columns: new[] { "UserId", "IsRead", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "idx_message_conversation",
                table: "Message",
                columns: new[] { "ConversationId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "idx_conversation_booth_last_message",
                table: "Conversations",
                columns: new[] { "BoothId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "idx_conversation_customer_last_message",
                table: "Conversations",
                columns: new[] { "CustomerId", "LastMessageAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_notification_user_read",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "idx_message_conversation",
                table: "Message");

            migrationBuilder.DropIndex(
                name: "idx_conversation_booth_last_message",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "idx_conversation_customer_last_message",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "idx_message_conversation",
                table: "Message",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_BoothId",
                table: "Conversations",
                column: "BoothId");
        }
    }
}
