using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RepairPackagePriceDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Package"" ALTER COLUMN ""Status"" SET DEFAULT 'Active';
                ALTER TABLE ""Package"" ALTER COLUMN ""CreatedAt"" SET DEFAULT now();
                ALTER TABLE ""Package"" ALTER COLUMN ""UpdatedAt"" SET DEFAULT now();
                ALTER TABLE ""Package"" ALTER COLUMN ""IsDeleted"" SET DEFAULT false;

                ALTER TABLE ""PackagePrice"" ALTER COLUMN ""CreatedAt"" SET DEFAULT now();
                ALTER TABLE ""PackagePrice"" ALTER COLUMN ""UpdatedAt"" SET DEFAULT now();
                ALTER TABLE ""PackagePrice"" ALTER COLUMN ""IsDeleted"" SET DEFAULT false;
                ALTER TABLE ""PackagePrice"" ALTER COLUMN ""DurationDays"" SET DEFAULT 30;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Repair migration: Down is intentionally a no-op.
        }
    }
}
