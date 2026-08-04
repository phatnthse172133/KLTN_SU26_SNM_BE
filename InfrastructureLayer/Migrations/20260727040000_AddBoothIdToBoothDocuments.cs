using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddBoothIdToBoothDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some databases already gained BoothId through the (edited) table-creation
            // migration, so every structural change here must tolerate both states.
            migrationBuilder.Sql("""
                ALTER TABLE "BoothDocuments" ADD COLUMN IF NOT EXISTS "BoothId" uuid;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_BoothDocuments_BoothId" ON "BoothDocuments" ("BoothId");
                """);

            // Recreate the FK so both pre-existing (NO ACTION) and missing states converge
            // on ON DELETE SET NULL.
            migrationBuilder.Sql("""
                ALTER TABLE "BoothDocuments" DROP CONSTRAINT IF EXISTS "FK_BoothDocuments_Booth_BoothId";
                ALTER TABLE "BoothDocuments"
                    ADD CONSTRAINT "FK_BoothDocuments_Booth_BoothId"
                    FOREIGN KEY ("BoothId") REFERENCES "Booth" ("Id") ON DELETE SET NULL;
                """);

            // New booth-owner uploads link straight to the booth without a registration row.
            migrationBuilder.Sql("""
                ALTER TABLE "BoothDocuments" ALTER COLUMN "RegistrationId" DROP NOT NULL;
                """);

            // The old column default 'Pending' is not a BoothDocumentStatus member
            // (PendingReview | Verified | Rejected) and breaks enum materialization.
            migrationBuilder.Sql("""
                UPDATE "BoothDocuments" SET "VerificationStatus" = 'PendingReview' WHERE "VerificationStatus" = 'Pending';
                ALTER TABLE "BoothDocuments" ALTER COLUMN "VerificationStatus" SET DEFAULT 'PendingReview';
                """);

            // Backfill: link existing documents to the booth created from their registration.
            migrationBuilder.Sql("""
                UPDATE "BoothDocuments" bd
                SET "BoothId" = b."Id"
                FROM "Booth" b
                WHERE b."RegistrationId" = bd."RegistrationId"
                  AND bd."BoothId" IS NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Booth" ADD COLUMN IF NOT EXISTS "LogoUrl" character varying(500);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Booth" DROP COLUMN IF EXISTS "LogoUrl";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "BoothDocuments" ALTER COLUMN "VerificationStatus" SET DEFAULT 'Pending';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "BoothDocuments" DROP CONSTRAINT IF EXISTS "FK_BoothDocuments_Booth_BoothId";
                DROP INDEX IF EXISTS "IX_BoothDocuments_BoothId";
                ALTER TABLE "BoothDocuments" DROP COLUMN IF EXISTS "BoothId";
                """);
        }
    }
}
