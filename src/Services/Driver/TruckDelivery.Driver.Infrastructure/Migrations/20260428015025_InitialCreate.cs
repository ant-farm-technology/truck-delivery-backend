using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Driver.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "driver");
            migrationBuilder.AlterDatabase().Annotation("MySql:CharSet", "utf8mb4");

            // All CREATE TABLE / CREATE INDEX use IF NOT EXISTS so this migration is safe
            // to replay on any database state (fresh server, switched server, or partial
            // previous run where DDL committed but __EFMigrationsHistory did not).

            // ── drivers ──────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `drivers` (
                    `Id`                  char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `Email`               varchar(255) CHARACTER SET utf8mb4 NOT NULL,
                    `FirstName`           varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `LastName`            varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `PhoneNumber`         varchar(20)  CHARACTER SET utf8mb4 NOT NULL,
                    `LicenseNumber`       varchar(50)  CHARACTER SET utf8mb4 NOT NULL,
                    `Status`              int NOT NULL DEFAULT 1,
                    `CurrentVehicleId`    char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `IsActive`            tinyint(1) NOT NULL DEFAULT 1,
                    `CreatedAt`           datetime(6) NOT NULL,
                    `UpdatedAt`           datetime(6) NOT NULL,
                    `LicenseGrade`        int NOT NULL DEFAULT 3,
                    `LicenseExpiryDate`   date NOT NULL DEFAULT '2099-12-31',
                    `DateOfBirth`         date NOT NULL DEFAULT '1990-01-01',
                    `Address`             varchar(500)  CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
                    `IdCardNumber`        varchar(20)   CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
                    `PortraitPhotoUrl`    varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `IdCardFrontUrl`      varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `IdCardBackUrl`       varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `LicenseFrontUrl`     varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `LicenseBackUrl`      varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `VehicleRegFrontUrl`  varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `VehicleRegBackUrl`   varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `VerificationStatus`  int NOT NULL DEFAULT 0,
                    `OcrConfidenceScore`  float NULL,
                    `VerificationNotes`   varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `TrustScore`          int NOT NULL DEFAULT 70,
                    CONSTRAINT `PK_drivers` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_drivers_Email`              ON `drivers` (`Email`);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_drivers_LicenseNumber`      ON `drivers` (`LicenseNumber`);");
            migrationBuilder.Sql("CREATE        INDEX IF NOT EXISTS `IX_drivers_Status`             ON `drivers` (`Status`);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_drivers_IdCardNumber`       ON `drivers` (`IdCardNumber`);");
            migrationBuilder.Sql("CREATE        INDEX IF NOT EXISTS `IX_drivers_VerificationStatus` ON `drivers` (`VerificationStatus`);");

            // ── outbox_messages ───────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `outbox_messages` (
                    `Id`            char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `EventType`     varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `Topic`         varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `PartitionKey`  varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `Payload`       longtext CHARACTER SET utf8mb4 NOT NULL,
                    `OccurredAt`    datetime(6) NOT NULL,
                    `ProcessedAt`   datetime(6) NULL,
                    `RetryCount`    int NOT NULL DEFAULT 0,
                    `LastError`     varchar(1000) CHARACTER SET utf8mb4 NULL,
                    CONSTRAINT `PK_outbox_messages` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_outbox_messages_ProcessedAt`            ON `outbox_messages` (`ProcessedAt`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_outbox_messages_ProcessedAt_RetryCount` ON `outbox_messages` (`ProcessedAt`, `RetryCount`);");

            // ── vehicles ──────────────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `vehicles` (
                    `Id`                     char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `LicensePlate`           varchar(20)  CHARACTER SET utf8mb4 NOT NULL,
                    `Brand`                  varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `Model`                  varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `Type`                   int NOT NULL,
                    `MaxWeightKg`            decimal(10,3) NOT NULL,
                    `MaxVolumeCbm`           decimal(10,3) NOT NULL,
                    `YearOfManufacture`      int NOT NULL,
                    `Status`                 int NOT NULL DEFAULT 1,
                    `AssignedDriverId`       char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `CreatedAt`              datetime(6) NOT NULL,
                    `UpdatedAt`              datetime(6) NOT NULL,
                    `LengthM`                decimal(8,3) NOT NULL DEFAULT 4.200,
                    `WidthM`                 decimal(8,3) NOT NULL DEFAULT 1.800,
                    `HeightM`                decimal(8,3) NOT NULL DEFAULT 1.800,
                    `RegistrationNumber`     varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
                    `RegistrationExpiryDate` date NOT NULL DEFAULT '2099-12-31',
                    CONSTRAINT `PK_vehicles` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_vehicles_LicensePlate`         ON `vehicles` (`LicensePlate`);");
            migrationBuilder.Sql("CREATE        INDEX IF NOT EXISTS `IX_vehicles_AssignedDriverId`     ON `vehicles` (`AssignedDriverId`);");
            migrationBuilder.Sql("CREATE        INDEX IF NOT EXISTS `IX_vehicles_Status`               ON `vehicles` (`Status`);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_vehicles_RegistrationNumber`   ON `vehicles` (`RegistrationNumber`);");

            // ── BreakdownReports ──────────────────────────────────────────────────────
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `BreakdownReports` (
                    `Id`             char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `DriverId`       char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `VehicleId`      char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `Latitude`       double NOT NULL,
                    `Longitude`      double NOT NULL,
                    `PhotoUrlsJson`  varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
                    `FraudRiskLevel` varchar(20)   CHARACTER SET utf8mb4 NOT NULL,
                    `ReviewNote`     varchar(500)  CHARACTER SET utf8mb4 NULL,
                    `ReportedAt`     datetime(6) NOT NULL,
                    CONSTRAINT `PK_BreakdownReports` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_BreakdownReports_DriverId`   ON `BreakdownReports` (`DriverId`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_BreakdownReports_ReportedAt` ON `BreakdownReports` (`ReportedAt`);");

            // ── DriverSwapRecords ─────────────────────────────────────────────────────
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

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_DriverSwapRecords_OriginalDriverId_ReplacementDriverId` ON `DriverSwapRecords` (`OriginalDriverId`, `ReplacementDriverId`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_DriverSwapRecords_OccurredAt`                          ON `DriverSwapRecords` (`OccurredAt`);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BreakdownReports");
            migrationBuilder.DropTable(name: "DriverSwapRecords");
            migrationBuilder.DropTable(name: "drivers");
            migrationBuilder.DropTable(name: "outbox_messages");
            migrationBuilder.DropTable(name: "vehicles");
        }
    }
}
