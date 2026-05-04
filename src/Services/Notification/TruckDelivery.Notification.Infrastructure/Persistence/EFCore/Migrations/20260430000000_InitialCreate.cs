using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Notification.Infrastructure.Persistence.EFCore.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase().Annotation("MySql:CharSet", "utf8mb4");

            // ── Notifications ─────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `Notifications` (
                    `Id`            char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `RecipientId`   char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `Type`          varchar(50)   CHARACTER SET utf8mb4 NOT NULL,
                    `Channel`       varchar(20)   CHARACTER SET utf8mb4 NOT NULL,
                    `Title`         varchar(200)  CHARACTER SET utf8mb4 NOT NULL,
                    `Body`          varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
                    `Status`        varchar(20)   CHARACTER SET utf8mb4 NOT NULL,
                    `FailureReason` varchar(500)  CHARACTER SET utf8mb4 NULL,
                    `CreatedAt`     datetime(6) NOT NULL,
                    `SentAt`        datetime(6) NULL,
                    CONSTRAINT `PK_Notifications` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_Notifications_CreatedAt`   ON `Notifications` (`CreatedAt`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_Notifications_RecipientId` ON `Notifications` (`RecipientId`);");

            // ── OutboxMessages ────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `OutboxMessages` (
                    `Id`           char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `EventType`    varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `Topic`        varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `PartitionKey` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `Payload`      longtext CHARACTER SET utf8mb4 NOT NULL,
                    `OccurredAt`   datetime(6) NOT NULL,
                    `ProcessedAt`  datetime(6) NULL,
                    `RetryCount`   int NOT NULL DEFAULT 0,
                    `LastError`    varchar(1000) CHARACTER SET utf8mb4 NULL,
                    CONSTRAINT `PK_OutboxMessages` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_OutboxMessages_ProcessedAt`            ON `OutboxMessages` (`ProcessedAt`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_OutboxMessages_ProcessedAt_RetryCount` ON `OutboxMessages` (`ProcessedAt`, `RetryCount`);");

            // ── device_tokens ─────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `device_tokens` (
                    `Id`           char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `UserId`       char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `Token`        varchar(500) CHARACTER SET utf8mb4 NOT NULL,
                    `Platform`     varchar(20)  CHARACTER SET utf8mb4 NOT NULL,
                    `RegisteredAt` datetime(6) NOT NULL,
                    CONSTRAINT `PK_device_tokens` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_device_tokens_UserId_Platform` ON `device_tokens` (`UserId`, `Platform`);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "device_tokens");
            migrationBuilder.DropTable(name: "Notifications");
            migrationBuilder.DropTable(name: "OutboxMessages");
        }
    }
}
