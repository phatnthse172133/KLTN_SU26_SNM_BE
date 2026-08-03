using System;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260717113421_AddAdminUserStatusManagement")]
public partial class AddAdminUserStatusManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "User"
            SET "Status" = CASE
                WHEN "Status"::text IN ('Banned', 'Suspended') THEN 'Inactive'
                WHEN "Status"::text IN ('Active', 'Inactive') THEN "Status"
                ELSE 'Inactive'
            END
            WHERE "Status"::text NOT IN ('Active', 'Inactive');
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "EmailOutbox" (
                "Id" uuid NOT NULL,
                "RecipientEmail" text NOT NULL,
                "Subject" text NOT NULL,
                "HtmlBody" text NOT NULL,
                "EmailType" text NOT NULL,
                "ReferenceId" uuid NOT NULL,
                "Status" text NOT NULL,
                "RetryCount" integer NOT NULL,
                "LastError" text,
                "NextRetryAt" timestamp with time zone,
                "SentAt" timestamp with time zone,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_EmailOutbox" PRIMARY KEY ("Id")
            );
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "UserStatusHistories" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "ChangedByAdminId" uuid NOT NULL,
                "PreviousStatus" character varying(20) NOT NULL,
                "NewStatus" character varying(20) NOT NULL,
                "Reason" character varying(1000) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_UserStatusHistories" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_UserStatusHistories_User_ChangedByAdminId"
                    FOREIGN KEY ("ChangedByAdminId") REFERENCES "User" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_UserStatusHistories_User_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
            );
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmailOutbox_ReferenceId_EmailType"
                ON "EmailOutbox" ("ReferenceId", "EmailType");
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_UserStatusHistories_ChangedByAdminId"
                ON "UserStatusHistories" ("ChangedByAdminId");
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_UserStatusHistories_UserId_CreatedAt"
                ON "UserStatusHistories" ("UserId", "CreatedAt");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EmailOutbox");
        migrationBuilder.DropTable(name: "UserStatusHistories");
    }
}
