using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Shipment.Infrastructure.Persistence.EFCore.Migrations
{
    public partial class AddCoordinatesToShipment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "pickup_lat",
                schema: "shipment",
                table: "shipments",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "pickup_lng",
                schema: "shipment",
                table: "shipments",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "delivery_lat",
                schema: "shipment",
                table: "shipments",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "delivery_lng",
                schema: "shipment",
                table: "shipments",
                type: "double",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "pickup_lat", schema: "shipment", table: "shipments");
            migrationBuilder.DropColumn(name: "pickup_lng", schema: "shipment", table: "shipments");
            migrationBuilder.DropColumn(name: "delivery_lat", schema: "shipment", table: "shipments");
            migrationBuilder.DropColumn(name: "delivery_lng", schema: "shipment", table: "shipments");
        }
    }
}
