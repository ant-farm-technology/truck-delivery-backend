using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Payment.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase().Annotation("MySql:CharSet", "utf8mb4");

            // ── Payments ──────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `Payments` (
                    `Id`            char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `OrderId`       char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `CustomerId`    char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `Amount`        decimal(18,2) NOT NULL,
                    `Currency`      varchar(10) CHARACTER SET utf8mb4 NOT NULL,
                    `Status`        varchar(20) CHARACTER SET utf8mb4 NOT NULL,
                    `FailureReason` varchar(500) CHARACTER SET utf8mb4 NULL,
                    `CreatedAt`     datetime(6) NOT NULL,
                    `UpdatedAt`     datetime(6) NOT NULL,
                    `Method`        varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Cod',
                    `DriverId`      char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_Payments` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE        INDEX IF NOT EXISTS `IX_Payments_CustomerId` ON `Payments` (`CustomerId`);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_Payments_OrderId`    ON `Payments` (`OrderId`);");
            migrationBuilder.Sql("CREATE        INDEX IF NOT EXISTS `IX_Payments_DriverId`   ON `Payments` (`DriverId`);");

            // ── EscrowPayments ────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `EscrowPayments` (
                    `Id`                  char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `ShipmentId`          char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `OrderId`             char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `OriginalDriverId`    char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `ReplacementDriverId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `LockedAmount`        decimal(18,2) NOT NULL,
                    `Currency`            varchar(10)  CHARACTER SET utf8mb4 NOT NULL,
                    `Status`              varchar(20)  CHARACTER SET utf8mb4 NOT NULL,
                    `ResolutionNote`      varchar(500) CHARACTER SET utf8mb4 NULL,
                    `LockedAt`            datetime(6) NOT NULL,
                    `ResolvedAt`          datetime(6) NULL,
                    CONSTRAINT `PK_EscrowPayments` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_EscrowPayments_ShipmentId` ON `EscrowPayments` (`ShipmentId`);");
            migrationBuilder.Sql("CREATE        INDEX IF NOT EXISTS `IX_EscrowPayments_Status`     ON `EscrowPayments` (`Status`);");

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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EscrowPayments");
            migrationBuilder.DropTable(name: "OutboxMessages");
            migrationBuilder.DropTable(name: "Payments");
        }
    }
}
