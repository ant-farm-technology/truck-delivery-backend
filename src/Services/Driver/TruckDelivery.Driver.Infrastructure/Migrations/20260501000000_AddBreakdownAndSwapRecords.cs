using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Driver.Infrastructure.Migrations
{
    public partial class AddBreakdownAndSwapRecords : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CREATE TABLE IF NOT EXISTS so the migration is safe to replay.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `BreakdownReports` (
                    `Id`            char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `DriverId`      char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `VehicleId`     char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `Latitude`      double NOT NULL,
                    `Longitude`     double NOT NULL,
                    `PhotoUrlsJson` varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
                    `FraudRiskLevel` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
                    `ReviewNote`    varchar(500) CHARACTER SET utf8mb4 NULL,
                    `ReportedAt`    datetime(6) NOT NULL,
                    CONSTRAINT `PK_BreakdownReports` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `DriverSwapRecords` (
                    `Id`                  char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `OriginalDriverId`    char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `ReplacementDriverId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `ShipmentId`          char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `OccurredAt`          datetime(6) NOT NULL,
                    CONSTRAINT `PK_DriverSwapRecords` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_BreakdownReports_DriverId` ON `BreakdownReports` (`DriverId`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_BreakdownReports_ReportedAt` ON `BreakdownReports` (`ReportedAt`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_DriverSwapRecords_OriginalDriverId_ReplacementDriverId` ON `DriverSwapRecords` (`OriginalDriverId`, `ReplacementDriverId`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_DriverSwapRecords_OccurredAt` ON `DriverSwapRecords` (`OccurredAt`);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BreakdownReports", schema: "driver");
            migrationBuilder.DropTable(name: "DriverSwapRecords", schema: "driver");
        }
    }
}
