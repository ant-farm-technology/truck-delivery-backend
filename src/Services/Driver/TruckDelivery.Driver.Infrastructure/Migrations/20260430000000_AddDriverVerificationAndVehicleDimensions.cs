using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Driver.Infrastructure.Migrations
{
    public partial class AddDriverVerificationAndVehicleDimensions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Driver: new fields ──────────────────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "LicenseGrade",
                schema: "driver",
                table: "drivers",
                type: "int",
                nullable: false,
                defaultValueSql: "3"); // C = 3 as safe default

            migrationBuilder.AddColumn<DateOnly>(
                name: "LicenseExpiryDate",
                schema: "driver",
                table: "drivers",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2099, 12, 31));

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                schema: "driver",
                table: "drivers",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1990, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "driver",
                table: "drivers",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IdCardNumber",
                schema: "driver",
                table: "drivers",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // Document photo URLs
            migrationBuilder.AddColumn<string>(
                name: "PortraitPhotoUrl",
                schema: "driver",
                table: "drivers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IdCardFrontUrl",
                schema: "driver",
                table: "drivers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IdCardBackUrl",
                schema: "driver",
                table: "drivers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LicenseFrontUrl",
                schema: "driver",
                table: "drivers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LicenseBackUrl",
                schema: "driver",
                table: "drivers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VehicleRegFrontUrl",
                schema: "driver",
                table: "drivers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "VehicleRegBackUrl",
                schema: "driver",
                table: "drivers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Verification fields
            migrationBuilder.AddColumn<int>(
                name: "VerificationStatus",
                schema: "driver",
                table: "drivers",
                type: "int",
                nullable: false,
                defaultValueSql: "0");

            migrationBuilder.AddColumn<float>(
                name: "OcrConfidenceScore",
                schema: "driver",
                table: "drivers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationNotes",
                schema: "driver",
                table: "drivers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Also add TrustScore if missing (may already exist from Phase 5)
            migrationBuilder.Sql(
                "ALTER TABLE `drivers` ADD COLUMN IF NOT EXISTS `TrustScore` int NOT NULL DEFAULT 70;");

            // Unique index on IdCardNumber
            migrationBuilder.CreateIndex(
                name: "IX_drivers_IdCardNumber",
                schema: "driver",
                table: "drivers",
                column: "IdCardNumber",
                unique: true);

            // Index on VerificationStatus for admin queue queries
            migrationBuilder.CreateIndex(
                name: "IX_drivers_VerificationStatus",
                schema: "driver",
                table: "drivers",
                column: "VerificationStatus");

            // ── Vehicle: new fields ─────────────────────────────────────
            migrationBuilder.AddColumn<decimal>(
                name: "LengthM",
                schema: "driver",
                table: "vehicles",
                type: "decimal(8,3)",
                nullable: false,
                defaultValue: 4.2m);

            migrationBuilder.AddColumn<decimal>(
                name: "WidthM",
                schema: "driver",
                table: "vehicles",
                type: "decimal(8,3)",
                nullable: false,
                defaultValue: 1.8m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightM",
                schema: "driver",
                table: "vehicles",
                type: "decimal(8,3)",
                nullable: false,
                defaultValue: 1.8m);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                schema: "driver",
                table: "vehicles",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateOnly>(
                name: "RegistrationExpiryDate",
                schema: "driver",
                table: "vehicles",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2099, 12, 31));

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_RegistrationNumber",
                schema: "driver",
                table: "vehicles",
                column: "RegistrationNumber",
                unique: true);
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
