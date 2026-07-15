using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class EditForPaymentAndOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Order\" ALTER COLUMN \"OrderCode\" TYPE bigint USING (case when \"OrderCode\" ~ '^\\d+$' then \"OrderCode\"::bigint else 0 end);");

            migrationBuilder.DropForeignKey(
                name: "FK_Order_User_BoothOwnerId",
                table: "Order");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentMethods_User_UserId",
                table: "PaymentMethods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentMethods",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Gateway",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PayStatus",
                table: "Order");

            migrationBuilder.RenameTable(
                name: "PaymentMethods",
                newName: "PaymentMethod");

            migrationBuilder.RenameIndex(
                name: "IX_PaymentMethods_UserId",
                table: "PaymentMethod",
                newName: "IX_PaymentMethod_UserId");

            migrationBuilder.AlterTable(
                name: "Order",
                comment: "Đơn hàng của khách (1 đơn chỉ thuộc về 1 quán)",
                oldComment: "Đơn hàng của khách");

            migrationBuilder.AlterTable(
                name: "PaymentMethod",
                comment: "Cấu hình phương thức thanh toán ưu tiên của người dùng");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "Tiền mặt hoặc PayOS",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "Payment: thu tiền | Refund: hoàn tiền");

            migrationBuilder.AlterColumn<string>(
                name: "RefundReason",
                table: "Payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                comment: "Lý do hoàn tiền (Nếu có)",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaidAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Thời điểm dòng tiền thực tế được khách hàng quét mã và bắn về hệ thống thành công",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GatewayRef",
                table: "Payments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                comment: "Mã tra soát thực tế của ngân hàng (Ví dụ mã giao dịch của BIDV...)",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true,
                oldComment: "Mã tham chiếu từ cổng thanh toán bên thứ 3 - dùng để tra soát/khiếu nại");

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "Payments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                comment: "Đường link thanh toán VietQR động ngắn hạn do PayOS trả về");

            migrationBuilder.AddColumn<string>(
                name: "PaymentLinkId",
                table: "Payments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                comment: "ID quản lý liên kết link thanh toán của hệ thống PayOS");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Order",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "'Placed'::character varying",
                comment: "Placed | Preparing | ReadyForPickup | Completed | Cancelled",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "'Pending'::character varying",
                oldComment: "Pending | Confirmed | Preparing | Completed | Cancelled");

            migrationBuilder.AlterColumn<long>(
                name: "OrderCode",
                table: "Order",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentToken",
                table: "PaymentMethod",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "MethodType",
                table: "PaymentMethod",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentMethod",
                table: "PaymentMethod",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "Order_BoothOwnerId_fkey",
                table: "Order",
                column: "BoothOwnerId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentMethod_User_UserId",
                table: "PaymentMethod",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Order_BoothOwnerId_fkey",
                table: "Order");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentMethod_User_UserId",
                table: "PaymentMethod");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentMethod",
                table: "PaymentMethod");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentLinkId",
                table: "Payments");

            migrationBuilder.RenameTable(
                name: "PaymentMethod",
                newName: "PaymentMethods");

            migrationBuilder.RenameIndex(
                name: "IX_PaymentMethod_UserId",
                table: "PaymentMethods",
                newName: "IX_PaymentMethods_UserId");

            migrationBuilder.AlterTable(
                name: "Order",
                comment: "Đơn hàng của khách",
                oldComment: "Đơn hàng của khách (1 đơn chỉ thuộc về 1 quán)");

            migrationBuilder.AlterTable(
                name: "PaymentMethods",
                oldComment: "Cấu hình phương thức thanh toán ưu tiên của người dùng");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "Payment: thu tiền | Refund: hoàn tiền",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "Tiền mặt hoặc PayOS");

            migrationBuilder.AlterColumn<string>(
                name: "RefundReason",
                table: "Payments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true,
                oldComment: "Lý do hoàn tiền (Nếu có)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PaidAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Thời điểm dòng tiền thực tế được khách hàng quét mã và bắn về hệ thống thành công");

            migrationBuilder.AlterColumn<string>(
                name: "GatewayRef",
                table: "Payments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                comment: "Mã tham chiếu từ cổng thanh toán bên thứ 3 - dùng để tra soát/khiếu nại",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true,
                oldComment: "Mã tra soát thực tế của ngân hàng (Ví dụ mã giao dịch của BIDV...)");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValueSql: "'VND'::character varying");

            migrationBuilder.AddColumn<string>(
                name: "Gateway",
                table: "Payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Order",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Pending'::character varying",
                comment: "Pending | Confirmed | Preparing | Completed | Cancelled",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValueSql: "'Placed'::character varying",
                oldComment: "Placed | Preparing | ReadyForPickup | Completed | Cancelled");

            migrationBuilder.AlterColumn<string>(
                name: "OrderCode",
                table: "Order",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<int>(
                name: "PayStatus",
                table: "Order",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentToken",
                table: "PaymentMethods",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MethodType",
                table: "PaymentMethods",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentMethods",
                table: "PaymentMethods",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Order_User_BoothOwnerId",
                table: "Order",
                column: "BoothOwnerId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentMethods_User_UserId",
                table: "PaymentMethods",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
