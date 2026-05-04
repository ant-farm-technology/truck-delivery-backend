using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Order.Infrastructure.Migrations
{
    public partial class AddShipmentIdToOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShipmentId",
                schema: "order",
                table: "orders",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ShipmentId",
                schema: "order",
                table: "orders",
                column: "ShipmentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_orders_ShipmentId", schema: "order", table: "orders");
            migrationBuilder.DropColumn(name: "ShipmentId", schema: "order", table: "orders");
        }
    }
}
