using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class RepairNotificationAndPackageDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Repair Notification table: add any missing columns (idempotent).
            migrationBuilder.Sql(@"
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""IsRead"" boolean NOT NULL DEFAULT false;
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""ReadAt"" timestamp with time zone NULL;
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""ReferenceType"" character varying(100) NULL;
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""ReferenceId"" uuid NULL;
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""DataJson"" text NULL;
                ALTER TABLE ""Notification"" ADD COLUMN IF NOT EXISTS ""IsDeleted"" boolean NOT NULL DEFAULT false;
            ");

            // 2) Repair UserDeviceToken table if missing.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""UserDeviceToken"" (
                    ""Id"" uuid NOT NULL DEFAULT uuid_generate_v4(),
                    ""UserId"" uuid NOT NULL,
                    ""Token"" character varying(4096) NOT NULL,
                    ""Platform"" character varying(20) NOT NULL,
                    ""DeviceId"" character varying(200) NULL,
                    ""IsActive"" boolean NOT NULL DEFAULT true,
                    ""IsDeleted"" boolean NOT NULL DEFAULT false,
                    ""LastUsedAt"" timestamp with time zone NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""UserDeviceToken_pkey"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""UserDeviceToken_UserId_fkey"" FOREIGN KEY (""UserId"") REFERENCES ""User""(""Id"") ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""UserDeviceToken_Token_key"" ON ""UserDeviceToken"" (""Token"");
                CREATE INDEX IF NOT EXISTS ""idx_device_token_user_active"" ON ""UserDeviceToken"" (""UserId"", ""IsActive"");
                COMMENT ON TABLE ""UserDeviceToken"" IS 'FCM device tokens registered by users';
            ");

            // 3) Restore Package DB defaults that match the EF model configuration.
            //    EF is instructed via HasDefaultValueSql(...) that the DB will supply these,
            //    so when the CLR default value is used (e.g. PackageStatus.Active = 0), EF
            //    omits the column from INSERT. Without a real DB default the insert fails
            //    with a NOT NULL violation. This makes the DB match the model.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Package"" ALTER COLUMN ""Status"" SET DEFAULT 'Active';
                ALTER TABLE ""Package"" ALTER COLUMN ""CreatedAt"" SET DEFAULT now();
                ALTER TABLE ""Package"" ALTER COLUMN ""UpdatedAt"" SET DEFAULT now();
                ALTER TABLE ""Package"" ALTER COLUMN ""IsDeleted"" SET DEFAULT false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Repair migration: Down is intentionally a no-op. Dropping columns/tables
            // or defaults here would risk data loss and cannot un-repair a broken DB.
        }
    }
}
