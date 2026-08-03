using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class InitialAdminNotificationBatchMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""BatchId"" uuid;
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""CreatedByUserId"" uuid;
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""Target"" integer;
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""TargetRole"" character varying(50);
                
                CREATE INDEX IF NOT EXISTS idx_notification_batch_id ON ""Notification"" (""BatchId"");
                CREATE INDEX IF NOT EXISTS idx_notification_created_by_created_at ON ""Notification"" (""CreatedByUserId"", ""CreatedAt"" DESC);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_notification_batch_id",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "idx_notification_created_by_created_at",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "Target",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "TargetRole",
                table: "Notification");
        }
    }
}
