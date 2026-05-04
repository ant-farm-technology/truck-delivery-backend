using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Order.Infrastructure.Migrations
{
    public partial class AddCoordinatesToOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PickupLatitude",
                schema: "order",
                table: "orders",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLongitude",
                schema: "order",
                table: "orders",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryLatitude",
                schema: "order",
                table: "orders",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryLongitude",
                schema: "order",
                table: "orders",
                type: "double",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PickupLatitude", schema: "order", table: "orders");
            migrationBuilder.DropColumn(name: "PickupLongitude", schema: "order", table: "orders");
            migrationBuilder.DropColumn(name: "DeliveryLatitude", schema: "order", table: "orders");
            migrationBuilder.DropColumn(name: "DeliveryLongitude", schema: "order", table: "orders");
        }
    }
}
