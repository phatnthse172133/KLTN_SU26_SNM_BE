using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPackagePolicyPartialUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Version",
                table: "PackagePolicies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "PackagePolicies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "PackagePolicies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "PackagePolicies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            // Keep the most recently effective non-deleted policy active for each package.
            // This makes the unique index safe for databases that contain legacy duplicates.
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT ""Id"",
                           ROW_NUMBER() OVER (
                               PARTITION BY ""PackageId""
                               ORDER BY ""EffectiveFrom"" DESC, ""UpdatedAt"" DESC, ""CreatedAt"" DESC, ""Id""
                           ) AS row_number
                    FROM ""PackagePolicies""
                    WHERE ""IsActive"" = TRUE AND ""IsDeleted"" = FALSE
                )
                UPDATE ""PackagePolicies"" AS policy
                SET ""IsActive"" = FALSE,
                    ""UpdatedAt"" = now()
                FROM ranked
                WHERE policy.""Id"" = ranked.""Id""
                  AND ranked.row_number > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_PackagePolicies_PackageId_IsActive",
                table: "PackagePolicies",
                columns: new[] { "PackageId", "IsActive" },
                unique: true,
                filter: "\"IsActive\" = true AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PackagePolicies_PackageId_IsActive",
                table: "PackagePolicies");

            migrationBuilder.AlterColumn<string>(
                name: "Version",
                table: "PackagePolicies",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "PackagePolicies",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "PackagePolicies",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "PackagePolicies",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");
        }
    }
}
