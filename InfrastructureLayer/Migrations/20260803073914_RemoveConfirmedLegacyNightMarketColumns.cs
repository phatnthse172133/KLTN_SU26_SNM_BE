using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveConfirmedLegacyNightMarketColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    has_data boolean;
                    legacy_column text;
                BEGIN
                    FOREACH legacy_column IN ARRAY ARRAY[
                        'ModerationNotes',
                        'RejectedReason',
                        'LiveStreamUrl',
                        'LiveStreamStartedAt',
                        'LiveStreamEndedAt'
                    ]
                    LOOP
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'NightMarket'
                              AND information_schema.columns.column_name = legacy_column
                        ) THEN
                            EXECUTE format(
                                'SELECT EXISTS (SELECT 1 FROM "NightMarket" WHERE %I IS NOT NULL)',
                                legacy_column
                            ) INTO has_data;
                            IF has_data THEN
                                RAISE EXCEPTION 'Legacy NightMarket column % contains data. Cleanup aborted.', legacy_column;
                            END IF;
                        END IF;
                    END LOOP;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'NightMarket'
                          AND information_schema.columns.column_name = 'HasLiveStream'
                    ) THEN
                        EXECUTE 'SELECT EXISTS (SELECT 1 FROM "NightMarket" WHERE "HasLiveStream" = true)'
                            INTO has_data;
                        IF has_data THEN
                            RAISE EXCEPTION 'Legacy NightMarket column HasLiveStream contains active data. Cleanup aborted.';
                        END IF;
                    END IF;
                END $$;

                ALTER TABLE "NightMarket"
                    DROP COLUMN IF EXISTS "ModerationNotes",
                    DROP COLUMN IF EXISTS "RejectedReason",
                    DROP COLUMN IF EXISTS "HasLiveStream",
                    DROP COLUMN IF EXISTS "LiveStreamUrl",
                    DROP COLUMN IF EXISTS "LiveStreamStartedAt",
                    DROP COLUMN IF EXISTS "LiveStreamEndedAt";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "NightMarket"
                    ADD COLUMN IF NOT EXISTS "ModerationNotes" text,
                    ADD COLUMN IF NOT EXISTS "RejectedReason" text,
                    ADD COLUMN IF NOT EXISTS "HasLiveStream" boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS "LiveStreamUrl" text,
                    ADD COLUMN IF NOT EXISTS "LiveStreamStartedAt" timestamp with time zone,
                    ADD COLUMN IF NOT EXISTS "LiveStreamEndedAt" timestamp with time zone;
                """);
        }
    }
}
