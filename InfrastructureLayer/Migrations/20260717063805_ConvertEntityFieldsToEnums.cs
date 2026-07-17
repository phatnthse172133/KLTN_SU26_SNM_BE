using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class ConvertEntityFieldsToEnums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "User"
                SET "AuthProvider" = 'LocalGoogle'
                WHERE "AuthProvider" = 'Local,Google';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "User"
                SET "AuthProvider" = 'Local,Google'
                WHERE "AuthProvider" = 'LocalGoogle';
                """);
        }
    }
}
