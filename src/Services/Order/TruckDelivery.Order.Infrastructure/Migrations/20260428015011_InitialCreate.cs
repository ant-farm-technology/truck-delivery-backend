using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Order.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "order");
            migrationBuilder.AlterDatabase().Annotation("MySql:CharSet", "utf8mb4");

            // ── orders ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `orders` (
                    `Id`                  char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `CustomerId`          char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `PickupStreet`        varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `PickupCity`          varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `PickupProvince`      varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `PickupPostalCode`    varchar(20)  CHARACTER SET utf8mb4 NOT NULL,
                    `PickupCountry`       varchar(10)  CHARACTER SET utf8mb4 NOT NULL,
                    `DeliveryStreet`      varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `DeliveryCity`        varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `DeliveryProvince`    varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `DeliveryPostalCode`  varchar(20)  CHARACTER SET utf8mb4 NOT NULL,
                    `DeliveryCountry`     varchar(10)  CHARACTER SET utf8mb4 NOT NULL,
                    `Status`             int NOT NULL DEFAULT 1,
                    `Notes`              varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `TotalWeightKg`      decimal(10,3) NOT NULL,
                    `TotalVolumeCbm`     decimal(10,3) NOT NULL,
                    `CreatedAt`          datetime(6) NOT NULL,
                    `UpdatedAt`          datetime(6) NOT NULL,
                    `CancellationReason` varchar(500) CHARACTER SET utf8mb4 NULL,
                    `ShipmentId`         char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `PickupLatitude`     double NULL,
                    `PickupLongitude`    double NULL,
                    `DeliveryLatitude`   double NULL,
                    `DeliveryLongitude`  double NULL,
                    CONSTRAINT `PK_orders` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_orders_CreatedAt`  ON `orders` (`CreatedAt`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_orders_CustomerId` ON `orders` (`CustomerId`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_orders_Status`     ON `orders` (`Status`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_orders_ShipmentId` ON `orders` (`ShipmentId`);");

            // ── order_items ───────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `order_items` (
                    `Id`          char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `OrderId`     char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `ProductName` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `Quantity`    int NOT NULL,
                    `WeightKg`    decimal(10,3) NOT NULL,
                    `VolumeCbm`   decimal(10,3) NOT NULL,
                    `Notes`       varchar(500) CHARACTER SET utf8mb4 NULL,
                    `CanTilt`     tinyint(1) NOT NULL DEFAULT 0,
                    `HeightM`     decimal(8,3) NULL,
                    `LengthM`     decimal(8,3) NULL,
                    `WidthM`      decimal(8,3) NULL,
                    CONSTRAINT `PK_order_items` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_order_items_orders_OrderId`
                        FOREIGN KEY (`OrderId`) REFERENCES `orders` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_order_items_OrderId` ON `order_items` (`OrderId`);");

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
            migrationBuilder.DropTable(name: "order_items");
            migrationBuilder.DropTable(name: "outbox_messages");
            migrationBuilder.DropTable(name: "orders");
        }
    }
}
