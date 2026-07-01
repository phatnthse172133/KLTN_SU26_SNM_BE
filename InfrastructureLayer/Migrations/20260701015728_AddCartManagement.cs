using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddCartManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cart",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Cart_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "Cart_CustomerId_fkey",
                        column: x => x.CustomerId,
                        principalTable: "User",
                        principalColumn: "Id");
                },
                comment: "Giỏ hàng hiện tại của khách hàng");

            migrationBuilder.CreateTable(
                name: "CartItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    CartId = table.Column<Guid>(type: "uuid", nullable: false),
                    FoodItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("CartItem_pkey", x => x.Id);
                    table.ForeignKey(
                        name: "CartItem_CartId_fkey",
                        column: x => x.CartId,
                        principalTable: "Cart",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "CartItem_FoodItemId_fkey",
                        column: x => x.FoodItemId,
                        principalTable: "FoodItem",
                        principalColumn: "Id");
                },
                comment: "Món ăn trong giỏ hàng");

            migrationBuilder.CreateIndex(
                name: "ux_cart_active_customer",
                table: "Cart",
                column: "CustomerId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "idx_cartitem_cart",
                table: "CartItem",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "idx_cartitem_fooditem",
                table: "CartItem",
                column: "FoodItemId");

            migrationBuilder.CreateIndex(
                name: "ux_cartitem_active_food",
                table: "CartItem",
                columns: new[] { "CartId", "FoodItemId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartItem");

            migrationBuilder.DropTable(
                name: "Cart");
        }
    }
}
