using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

/// <summary>
/// Complements the existing case-sensitive email key with the invariant used by
/// authentication and Google account linking: trimmed, case-insensitive email.
/// </summary>
[DbContext(typeof(SNMDbContext))]
[Migration("20260924090000_EnforceNormalizedUserEmailUniqueness")]
public partial class EnforceNormalizedUserEmailUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT lower(btrim("Email"))
                    FROM "User"
                    GROUP BY lower(btrim("Email"))
                    HAVING count(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Cannot enforce normalized User email uniqueness: case/whitespace duplicates exist.';
                END IF;
            END $$;

            CREATE UNIQUE INDEX "UX_User_NormalizedEmail"
                ON "User" (lower(btrim("Email")));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_User_NormalizedEmail\";");
    }
}
