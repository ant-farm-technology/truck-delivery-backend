using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Identity.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase().Annotation("MySql:CharSet", "utf8mb4");

            // ── users ─────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `users` (
                    `Id`                    char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `Email`                 varchar(256) CHARACTER SET utf8mb4 NOT NULL,
                    `PasswordHash`          varchar(256) CHARACTER SET utf8mb4 NOT NULL,
                    `FirstName`             varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `LastName`              varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `Role`                  int NOT NULL DEFAULT 1,
                    `IsActive`              tinyint(1) NOT NULL DEFAULT 1,
                    `CreatedAt`             datetime(6) NOT NULL,
                    `LastLoginAt`           datetime(6) NULL,
                    `RefreshToken`          varchar(512) CHARACTER SET utf8mb4 NULL,
                    `RefreshTokenExpiresAt` datetime(6) NULL,
                    `PhoneNumber`           varchar(20) CHARACTER SET utf8mb4 NULL,
                    `DateOfBirth`           date NULL,
                    CONSTRAINT `PK_users` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_users_Email` ON `users` (`Email`);");

            // ── outbox_messages ───────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `outbox_messages` (
                    `Id`           char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `EventType`    varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `Topic`        varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `PartitionKey` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `Payload`      longtext CHARACTER SET utf8mb4 NOT NULL,
                    `OccurredAt`   datetime(6) NOT NULL,
                    `ProcessedAt`  datetime(6) NULL,
                    `RetryCount`   int NOT NULL DEFAULT 0,
                    `LastError`    varchar(1000) CHARACTER SET utf8mb4 NULL,
                    CONSTRAINT `PK_outbox_messages` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_outbox_messages_ProcessedAt`            ON `outbox_messages` (`ProcessedAt`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_outbox_messages_ProcessedAt_RetryCount` ON `outbox_messages` (`ProcessedAt`, `RetryCount`);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "outbox_messages");
            migrationBuilder.DropTable(name: "users");
        }
    }
}
