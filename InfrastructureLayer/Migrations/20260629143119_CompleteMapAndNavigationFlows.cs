using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMapAndNavigationFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "BoothLocations_BoothId_key",
                table: "BoothLocations");

            migrationBuilder.AddColumn<bool>(
                name: "IsAccessible",
                table: "LayoutNodes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "LayoutNodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStartingPoint",
                table: "LayoutNodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NodeType",
                table: "LayoutNodes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "'Junction'::character varying");

            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "LayoutNodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAccessible",
                table: "LayoutEdges",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBidirectional",
                table: "LayoutEdges",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "LayoutEdges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LayoutId",
                table: "LayoutEdges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "BoothLocations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LayoutNodeId",
                table: "BoothLocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                table: "BoothLocations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlotNumber",
                table: "BoothLocations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "BoothLocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "LayoutEdges" AS edge
                SET "LayoutId" = node."LayoutId"
                FROM "LayoutNodes" AS node
                WHERE node."Id" = edge."FromNodeId";

                INSERT INTO "LayoutNodes"
                    ("Id", "LayoutId", "NodeName", "NodeType", "XCoordinate", "YCoordinate",
                     "IsAccessible", "IsStartingPoint", "IsDeleted", "CreatedAt", "UpdatedAt")
                SELECT location."Id", location."LayoutId", 'Legacy booth access', 'BoothAccess',
                       location."XCoordinate", location."YCoordinate", true, false, false,
                       location."CreatedAt", location."UpdatedAt"
                FROM "BoothLocations" AS location
                WHERE NOT EXISTS (
                    SELECT 1 FROM "LayoutNodes" AS node WHERE node."Id" = location."Id");

                UPDATE "BoothLocations"
                SET "LayoutNodeId" = "Id";

                WITH duplicates AS (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "LayoutId", "FromNodeId", "ToNodeId"
                        ORDER BY "CreatedAt", "Id") AS row_number
                    FROM "LayoutEdges"
                )
                UPDATE "LayoutEdges" AS edge
                SET "IsDeleted" = true
                FROM duplicates
                WHERE edge."Id" = duplicates."Id" AND duplicates.row_number > 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "LayoutId",
                table: "LayoutEdges",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "LayoutNodeId",
                table: "BoothLocations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LayoutNodes_ZoneId",
                table: "LayoutNodes",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutEdges_FromNodeId",
                table: "LayoutEdges",
                column: "FromNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_LayoutEdges_ToNodeId",
                table: "LayoutEdges",
                column: "ToNodeId");

            migrationBuilder.CreateIndex(
                name: "ux_layoutedge_active",
                table: "LayoutEdges",
                columns: new[] { "LayoutId", "FromNodeId", "ToNodeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_BoothLocations_ZoneId",
                table: "BoothLocations",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "ux_boothlocation_active_booth",
                table: "BoothLocations",
                column: "BoothId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ux_boothlocation_active_node",
                table: "BoothLocations",
                column: "LayoutNodeId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "BoothLocations_LayoutNodeId_fkey",
                table: "BoothLocations",
                column: "LayoutNodeId",
                principalTable: "LayoutNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "BoothLocations_ZoneId_fkey",
                table: "BoothLocations",
                column: "ZoneId",
                principalTable: "Zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "LayoutEdges_FromNodeId_fkey",
                table: "LayoutEdges",
                column: "FromNodeId",
                principalTable: "LayoutNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "LayoutEdges_LayoutId_fkey",
                table: "LayoutEdges",
                column: "LayoutId",
                principalTable: "MarketLayouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "LayoutEdges_ToNodeId_fkey",
                table: "LayoutEdges",
                column: "ToNodeId",
                principalTable: "LayoutNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "LayoutNodes_ZoneId_fkey",
                table: "LayoutNodes",
                column: "ZoneId",
                principalTable: "Zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "BoothLocations_LayoutNodeId_fkey",
                table: "BoothLocations");

            migrationBuilder.DropForeignKey(
                name: "BoothLocations_ZoneId_fkey",
                table: "BoothLocations");

            migrationBuilder.DropForeignKey(
                name: "LayoutEdges_FromNodeId_fkey",
                table: "LayoutEdges");

            migrationBuilder.DropForeignKey(
                name: "LayoutEdges_LayoutId_fkey",
                table: "LayoutEdges");

            migrationBuilder.DropForeignKey(
                name: "LayoutEdges_ToNodeId_fkey",
                table: "LayoutEdges");

            migrationBuilder.DropForeignKey(
                name: "LayoutNodes_ZoneId_fkey",
                table: "LayoutNodes");

            migrationBuilder.DropIndex(
                name: "IX_LayoutNodes_ZoneId",
                table: "LayoutNodes");

            migrationBuilder.DropIndex(
                name: "IX_LayoutEdges_FromNodeId",
                table: "LayoutEdges");

            migrationBuilder.DropIndex(
                name: "IX_LayoutEdges_ToNodeId",
                table: "LayoutEdges");

            migrationBuilder.DropIndex(
                name: "ux_layoutedge_active",
                table: "LayoutEdges");

            migrationBuilder.DropIndex(
                name: "IX_BoothLocations_ZoneId",
                table: "BoothLocations");

            migrationBuilder.DropIndex(
                name: "ux_boothlocation_active_booth",
                table: "BoothLocations");

            migrationBuilder.DropIndex(
                name: "ux_boothlocation_active_node",
                table: "BoothLocations");

            migrationBuilder.Sql(
                """
                DELETE FROM "LayoutNodes" AS node
                USING "BoothLocations" AS location
                WHERE node."Id" = location."LayoutNodeId"
                  AND node."NodeName" = 'Legacy booth access';
                """);

            migrationBuilder.DropColumn(
                name: "IsAccessible",
                table: "LayoutNodes");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "LayoutNodes");

            migrationBuilder.DropColumn(
                name: "IsStartingPoint",
                table: "LayoutNodes");

            migrationBuilder.DropColumn(
                name: "NodeType",
                table: "LayoutNodes");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "LayoutNodes");

            migrationBuilder.DropColumn(
                name: "IsAccessible",
                table: "LayoutEdges");

            migrationBuilder.DropColumn(
                name: "IsBidirectional",
                table: "LayoutEdges");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "LayoutEdges");

            migrationBuilder.DropColumn(
                name: "LayoutId",
                table: "LayoutEdges");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "BoothLocations");

            migrationBuilder.DropColumn(
                name: "LayoutNodeId",
                table: "BoothLocations");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "BoothLocations");

            migrationBuilder.DropColumn(
                name: "SlotNumber",
                table: "BoothLocations");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "BoothLocations");

            migrationBuilder.CreateIndex(
                name: "BoothLocations_BoothId_key",
                table: "BoothLocations",
                column: "BoothId",
                unique: true);
        }
    }
}
