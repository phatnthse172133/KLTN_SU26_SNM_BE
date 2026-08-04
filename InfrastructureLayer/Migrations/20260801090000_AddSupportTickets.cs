using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations;

[DbContext(typeof(SNMDbContext))]
[Migration("20260801090000_AddSupportTickets")]
public partial class AddSupportTickets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "SupportTickets" (
                "Id" uuid NOT NULL,
                "TicketCode" character varying(24) NOT NULL,
                "RequesterId" uuid NOT NULL,
                "RequesterRole" character varying(30) NOT NULL,
                "BoothId" uuid NULL,
                "NightMarketId" uuid NULL,
                "Category" character varying(50) NOT NULL,
                "Title" character varying(200) NOT NULL,
                "Description" character varying(4000) NOT NULL,
                "Status" character varying(30) NOT NULL,
                "Priority" character varying(20) NOT NULL,
                "PageUrl" character varying(1000) NULL,
                "AssignedAdminId" uuid NULL,
                "DueAt" timestamp with time zone NOT NULL,
                "FirstRespondedAt" timestamp with time zone NULL,
                "ResolvedAt" timestamp with time zone NULL,
                "ClosedAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_SupportTickets" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SupportTickets_User_RequesterId" FOREIGN KEY ("RequesterId") REFERENCES "User" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_SupportTickets_User_AssignedAdminId" FOREIGN KEY ("AssignedAdminId") REFERENCES "User" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SupportTickets_TicketCode" ON "SupportTickets" ("TicketCode");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_RequesterId_CreatedAt" ON "SupportTickets" ("RequesterId", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_Status_DueAt" ON "SupportTickets" ("Status", "DueAt");

            CREATE TABLE IF NOT EXISTS "SupportMessages" (
                "Id" uuid NOT NULL,
                "TicketId" uuid NOT NULL,
                "SenderId" uuid NOT NULL,
                "SenderRole" character varying(30) NOT NULL,
                "Body" character varying(4000) NOT NULL,
                "IsInternalNote" boolean NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_SupportMessages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SupportMessages_SupportTickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES "SupportTickets" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SupportMessages_User_SenderId" FOREIGN KEY ("SenderId") REFERENCES "User" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_SupportMessages_TicketId" ON "SupportMessages" ("TicketId");

            CREATE TABLE IF NOT EXISTS "SupportAttachments" (
                "Id" uuid NOT NULL,
                "TicketId" uuid NOT NULL,
                "MessageId" uuid NULL,
                "FileUrl" character varying(1000) NOT NULL,
                "OriginalFileName" character varying(255) NOT NULL,
                "ContentType" character varying(100) NOT NULL,
                "FileSize" bigint NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_SupportAttachments" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SupportAttachments_SupportTickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES "SupportTickets" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SupportAttachments_SupportMessages_MessageId" FOREIGN KEY ("MessageId") REFERENCES "SupportMessages" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_SupportAttachments_TicketId" ON "SupportAttachments" ("TicketId");
            CREATE INDEX IF NOT EXISTS "IX_SupportAttachments_MessageId" ON "SupportAttachments" ("MessageId");

            CREATE TABLE IF NOT EXISTS "SupportStatusHistories" (
                "Id" uuid NOT NULL,
                "TicketId" uuid NOT NULL,
                "ActorId" uuid NOT NULL,
                "FromStatus" character varying(30) NULL,
                "ToStatus" character varying(30) NOT NULL,
                "Note" character varying(1000) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_SupportStatusHistories" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SupportStatusHistories_SupportTickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES "SupportTickets" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SupportStatusHistories_User_ActorId" FOREIGN KEY ("ActorId") REFERENCES "User" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_SupportStatusHistories_TicketId" ON "SupportStatusHistories" ("TicketId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "SupportAttachments";
            DROP TABLE IF EXISTS "SupportStatusHistories";
            DROP TABLE IF EXISTS "SupportMessages";
            DROP TABLE IF EXISTS "SupportTickets";
            """);
    }
}
