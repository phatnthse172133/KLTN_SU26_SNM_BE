using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    public partial class AddCheckoutIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CheckoutRequestId",
                table: "Order",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_order_customer_checkout_request",
                table: "Order",
                columns: new[] { "CustomerId", "CheckoutRequestId" },
                unique: true,
                filter: "\"CheckoutRequestId\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_order_customer_checkout_request",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "CheckoutRequestId",
                table: "Order");
        }
    }
}
