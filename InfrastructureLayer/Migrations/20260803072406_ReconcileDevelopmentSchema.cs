using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <summary>
    /// Reconciles the EF model snapshot after migration files that were created
    /// without complete designer metadata. The preceding migration chain already
    /// creates the represented schema, so this migration intentionally performs
    /// no database DDL.
    /// </summary>
    public partial class ReconcileDevelopmentSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. Schema parity is verified by fresh-database and
            // restored-development-clone migration tests before this is published.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty because Up does not mutate the database schema.
        }
    }
}
