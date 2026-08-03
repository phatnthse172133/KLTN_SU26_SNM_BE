using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class MakeBoothRegistrationIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Booth_BoothRegistrations_RegistrationId",
                table: "Booth");

            migrationBuilder.DropIndex(
                name: "IX_Booth_RegistrationId",
                table: "Booth");

            migrationBuilder.AlterColumn<Guid>(
                name: "RegistrationId",
                table: "Booth",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_Booth_RegistrationId",
                table: "Booth",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "Booth_RegistrationId_fkey",
                table: "Booth",
                column: "RegistrationId",
                principalTable: "BoothRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "Booth_RegistrationId_fkey",
                table: "Booth");

            migrationBuilder.DropIndex(
                name: "IX_Booth_RegistrationId",
                table: "Booth");

            migrationBuilder.AlterColumn<Guid>(
                name: "RegistrationId",
                table: "Booth",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Booth_RegistrationId",
                table: "Booth",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Booth_BoothRegistrations_RegistrationId",
                table: "Booth",
                column: "RegistrationId",
                principalTable: "BoothRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
