using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageCodeEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "PackagePrice",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Package",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Entitlements",
                table: "Package",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Package_Code",
                table: "Package",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_BoothSubscriptions_BoothId_Active""
ON ""BoothSubscriptions"" (""BoothId"")
WHERE ""Status"" = 'Active';
");

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_BoothSubscriptions_BoothId_PendingPayment""
ON ""BoothSubscriptions"" (""BoothId"")
WHERE ""Status"" = 'PendingPayment';
");

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MarketSubscriptions_MarketOwnerId_Active""
ON ""MarketSubscriptions"" (""MarketOwnerId"")
WHERE ""Status"" = 'Active';
");

            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MarketSubscriptions_MarketOwnerId_PendingPayment""
ON ""MarketSubscriptions"" (""MarketOwnerId"")
WHERE ""Status"" = 'PendingPayment';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MarketSubscriptions_MarketOwnerId_PendingPayment"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_MarketSubscriptions_MarketOwnerId_Active"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_BoothSubscriptions_BoothId_PendingPayment"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_BoothSubscriptions_BoothId_Active"";");

            migrationBuilder.DropIndex(
                name: "IX_Package_Code",
                table: "Package");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "PackagePrice");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Package");

            migrationBuilder.DropColumn(
                name: "Entitlements",
                table: "Package");
        }
    }
}
