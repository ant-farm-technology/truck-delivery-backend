using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Shipment.Infrastructure.Persistence.EFCore.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatcherReviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bin_check_warnings",
                schema: "shipment",
                table: "shipments",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "packages_json",
                schema: "shipment",
                table: "shipments",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "requires_dispatcher_confirmation",
                schema: "shipment",
                table: "shipments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bin_check_warnings",
                schema: "shipment",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "packages_json",
                schema: "shipment",
                table: "shipments");

            migrationBuilder.DropColumn(
                name: "requires_dispatcher_confirmation",
                schema: "shipment",
                table: "shipments");
        }
    }
}
