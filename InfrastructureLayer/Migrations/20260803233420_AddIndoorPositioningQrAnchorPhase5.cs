using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddIndoorPositioningQrAnchorPhase5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsQrEnabled",
                table: "LayoutNavigationAnchors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicTokenHash",
                table: "LayoutNavigationAnchors",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QrValidFrom",
                table: "LayoutNavigationAnchors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QrValidUntil",
                table: "LayoutNavigationAnchors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "LayoutNavigationAnchors",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_navigationanchor_qr_hash",
                table: "LayoutNavigationAnchors",
                sql: "\"IsQrEnabled\" = false OR \"PublicTokenHash\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navigationanchor_qr_validity",
                table: "LayoutNavigationAnchors",
                sql: "\"QrValidFrom\" IS NULL OR \"QrValidUntil\" IS NULL OR \"QrValidFrom\" < \"QrValidUntil\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_navigationanchor_token_version",
                table: "LayoutNavigationAnchors",
                sql: "\"TokenVersion\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_navigationanchor_qr_hash",
                table: "LayoutNavigationAnchors");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navigationanchor_qr_validity",
                table: "LayoutNavigationAnchors");

            migrationBuilder.DropCheckConstraint(
                name: "ck_navigationanchor_token_version",
                table: "LayoutNavigationAnchors");

            migrationBuilder.DropColumn(
                name: "IsQrEnabled",
                table: "LayoutNavigationAnchors");

            migrationBuilder.DropColumn(
                name: "PublicTokenHash",
                table: "LayoutNavigationAnchors");

            migrationBuilder.DropColumn(
                name: "QrValidFrom",
                table: "LayoutNavigationAnchors");

            migrationBuilder.DropColumn(
                name: "QrValidUntil",
                table: "LayoutNavigationAnchors");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "LayoutNavigationAnchors");
        }
    }
}
