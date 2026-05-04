using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Shipment.Infrastructure.Persistence.EFCore.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "shipment");
            migrationBuilder.AlterDatabase().Annotation("MySql:CharSet", "utf8mb4");

            // ── shipments ─────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `shipments` (
                    `id`                              char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `order_id`                        char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `customer_id`                     char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `status`                          int NOT NULL DEFAULT 1,
                    `pickup_city`                     varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `pickup_province`                 varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `delivery_city`                   varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `delivery_province`               varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `total_weight_kg`                 decimal(10,3) NOT NULL,
                    `total_volume_cbm`                decimal(10,3) NOT NULL,
                    `assigned_driver_id`              char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `assigned_vehicle_id`             char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `route_distance_m`                double NULL,
                    `route_duration_s`                double NULL,
                    `route_polyline`                  varchar(5000) CHARACTER SET utf8mb4 NULL,
                    `failure_reason`                  varchar(500) CHARACTER SET utf8mb4 NULL,
                    `created_at`                      datetime(6) NOT NULL,
                    `updated_at`                      datetime(6) NOT NULL,
                    `bin_check_warnings`              varchar(2000) CHARACTER SET utf8mb4 NULL,
                    `packages_json`                   longtext CHARACTER SET utf8mb4 NULL,
                    `requires_dispatcher_confirmation` tinyint(1) NOT NULL DEFAULT 0,
                    `pickup_lat`                      double NULL,
                    `pickup_lng`                      double NULL,
                    `delivery_lat`                    double NULL,
                    `delivery_lng`                    double NULL,
                    CONSTRAINT `PK_shipments` PRIMARY KEY (`id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_shipments_order_id` ON `shipments` (`order_id`);");
            migrationBuilder.Sql("CREATE        INDEX IF NOT EXISTS `IX_shipments_status`   ON `shipments` (`status`);");

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
            migrationBuilder.DropTable(name: "shipments");
        }
    }
}
