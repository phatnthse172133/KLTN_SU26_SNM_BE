using System;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260818141000_RemoveAssistantConversationMarketId")]
public partial class RemoveAssistantConversationMarketId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AssistantConversation_NightMarket_MarketId",
            table: "AssistantConversation");

        migrationBuilder.DropIndex(
            name: "IX_AssistantConversation_MarketId",
            table: "AssistantConversation");

        migrationBuilder.DropColumn(
            name: "MarketId",
            table: "AssistantConversation");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "MarketId",
            table: "AssistantConversation",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AssistantConversation_MarketId",
            table: "AssistantConversation",
            column: "MarketId");

        migrationBuilder.AddForeignKey(
            name: "FK_AssistantConversation_NightMarket_MarketId",
            table: "AssistantConversation",
            column: "MarketId",
            principalTable: "NightMarket",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }
}
