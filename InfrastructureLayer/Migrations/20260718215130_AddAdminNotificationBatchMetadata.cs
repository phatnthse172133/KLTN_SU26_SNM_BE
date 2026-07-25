using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260718215130_AddAdminNotificationBatchMetadata")]
public partial class AddAdminNotificationBatchMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "BatchId" uuid;
            ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid;
            ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "Target" integer;
            ALTER TABLE "Notification" ADD COLUMN IF NOT EXISTS "TargetRole" character varying(50);

            CREATE INDEX IF NOT EXISTS idx_notification_batch_id
                ON "Notification" ("BatchId");
            CREATE INDEX IF NOT EXISTS idx_notification_created_by_created_at
                ON "Notification" ("CreatedByUserId", "CreatedAt" DESC);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS idx_notification_created_by_created_at;
            DROP INDEX IF EXISTS idx_notification_batch_id;

            ALTER TABLE "Notification" DROP COLUMN IF EXISTS "TargetRole";
            ALTER TABLE "Notification" DROP COLUMN IF EXISTS "Target";
            ALTER TABLE "Notification" DROP COLUMN IF EXISTS "CreatedByUserId";
            ALTER TABLE "Notification" DROP COLUMN IF EXISTS "BatchId";
            """);
    }
}
