using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddChatRealtimeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Message_SenderId",
                table: "Message");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientMessageId",
                table: "Message",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Message",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "Message",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BoothOwnerLastReadAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerLastReadAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMessageId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_message_sender_client_message",
                table: "Message",
                columns: new[] { "SenderId", "ClientMessageId" },
                unique: true,
                filter: "\"ClientMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_conversation_last_message",
                table: "Conversations",
                column: "LastMessageAt");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LastMessageId",
                table: "Conversations",
                column: "LastMessageId");

            migrationBuilder.AddForeignKey(
                name: "Conversations_LastMessageId_fkey",
                table: "Conversations",
                column: "LastMessageId",
                principalTable: "Message",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Conversations_LastMessageId_fkey",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "ux_message_sender_client_message",
                table: "Message");

            migrationBuilder.DropIndex(
                name: "idx_conversation_last_message",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_LastMessageId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ClientMessageId",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "Message");

            migrationBuilder.DropColumn(
                name: "BoothOwnerLastReadAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "CustomerLastReadAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "LastMessageAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "LastMessageId",
                table: "Conversations");

            migrationBuilder.CreateIndex(
                name: "IX_Message_SenderId",
                table: "Message",
                column: "SenderId");
        }
    }
}
