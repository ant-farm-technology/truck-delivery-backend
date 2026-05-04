using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Driver.Infrastructure.Migrations
{
    public partial class AddDriverVerificationAndVehicleDimensions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All DDL uses IF NOT EXISTS so re-applying this migration on a DB that already
            // has the columns (but missing the __EFMigrationsHistory row) is safe.

            // ── Driver: new columns ─────────────────────────────────────
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `LicenseGrade` int NOT NULL DEFAULT 3;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `LicenseExpiryDate` date NOT NULL DEFAULT '2099-12-31';");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `DateOfBirth` date NOT NULL DEFAULT '1990-01-01';");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `Address` varchar(500) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `IdCardNumber` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `PortraitPhotoUrl` varchar(1000) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `IdCardFrontUrl` varchar(1000) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `IdCardBackUrl` varchar(1000) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `LicenseFrontUrl` varchar(1000) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `LicenseBackUrl` varchar(1000) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `VehicleRegFrontUrl` varchar(1000) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `VehicleRegBackUrl` varchar(1000) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `VerificationStatus` int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `OcrConfidenceScore` float NULL;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `VerificationNotes` varchar(1000) CHARACTER SET utf8mb4 NULL;");
            migrationBuilder.Sql("ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `TrustScore` int NOT NULL DEFAULT 70;");

            // ── Driver: indexes ─────────────────────────────────────────
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_drivers_IdCardNumber` ON `drivers` (`IdCardNumber`);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS `IX_drivers_VerificationStatus` ON `drivers` (`VerificationStatus`);");

            // ── Vehicle: new columns ────────────────────────────────────
            migrationBuilder.Sql("ALTER TABLE `vehicles` ADD COLUMN IF NOT EXISTS `LengthM` decimal(8,3) NOT NULL DEFAULT 4.200;");
            migrationBuilder.Sql("ALTER TABLE `vehicles` ADD COLUMN IF NOT EXISTS `WidthM` decimal(8,3) NOT NULL DEFAULT 1.800;");
            migrationBuilder.Sql("ALTER TABLE `vehicles` ADD COLUMN IF NOT EXISTS `HeightM` decimal(8,3) NOT NULL DEFAULT 1.800;");
            migrationBuilder.Sql("ALTER TABLE `vehicles` ADD COLUMN IF NOT EXISTS `RegistrationNumber` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE `vehicles` ADD COLUMN IF NOT EXISTS `RegistrationExpiryDate` date NOT NULL DEFAULT '2099-12-31';");

            // ── Vehicle: indexes ────────────────────────────────────────
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS `IX_vehicles_RegistrationNumber` ON `vehicles` (`RegistrationNumber`);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_drivers_IdCardNumber", schema: "driver", table: "drivers");
            migrationBuilder.DropIndex(name: "IX_drivers_VerificationStatus", schema: "driver", table: "drivers");
            migrationBuilder.DropIndex(name: "IX_vehicles_RegistrationNumber", schema: "driver", table: "vehicles");

            migrationBuilder.DropColumn(name: "LicenseGrade", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "LicenseExpiryDate", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "DateOfBirth", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "Address", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "IdCardNumber", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "PortraitPhotoUrl", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "IdCardFrontUrl", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "IdCardBackUrl", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "LicenseFrontUrl", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "LicenseBackUrl", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "VehicleRegFrontUrl", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "VehicleRegBackUrl", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "VerificationStatus", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "OcrConfidenceScore", schema: "driver", table: "drivers");
            migrationBuilder.DropColumn(name: "VerificationNotes", schema: "driver", table: "drivers");

            migrationBuilder.DropColumn(name: "LengthM", schema: "driver", table: "vehicles");
            migrationBuilder.DropColumn(name: "WidthM", schema: "driver", table: "vehicles");
            migrationBuilder.DropColumn(name: "HeightM", schema: "driver", table: "vehicles");
            migrationBuilder.DropColumn(name: "RegistrationNumber", schema: "driver", table: "vehicles");
            migrationBuilder.DropColumn(name: "RegistrationExpiryDate", schema: "driver", table: "vehicles");
        }
    }
}
