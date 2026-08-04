using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileModerationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "ModerationActionHistory"
                        WHERE "AdminId" IS NULL
                           OR "PreviousStatus" IS NULL
                           OR "NewStatus" IS NULL
                           OR "Reason" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot reconcile ModerationActionHistory: required fields contain NULL values.';
                    END IF;
                END $$;

                ALTER TABLE "ModerationActionHistory"
                    ALTER COLUMN "AdminId" SET NOT NULL,
                    ALTER COLUMN "PreviousStatus" SET NOT NULL,
                    ALTER COLUMN "NewStatus" SET NOT NULL,
                    ALTER COLUMN "Reason" SET NOT NULL;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conrelid = '"ModerationActionHistory"'::regclass
                          AND conname = 'PK_ModerationActionHistory'
                    ) AND NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conrelid = '"ModerationActionHistory"'::regclass
                          AND conname = 'ModerationActionHistory_pkey'
                    ) THEN
                        ALTER TABLE "ModerationActionHistory"
                            RENAME CONSTRAINT "PK_ModerationActionHistory" TO "ModerationActionHistory_pkey";
                    END IF;
                END $$;

                CREATE INDEX IF NOT EXISTS "idx_nightmarket_moderation_status"
                    ON "NightMarket" ("ModerationStatus");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reconciliation is intentionally monotonic. The index can predate this
            // migration on a fresh database, so rollback must not remove it or weaken
            // required moderation history columns.
        }
    }
}
