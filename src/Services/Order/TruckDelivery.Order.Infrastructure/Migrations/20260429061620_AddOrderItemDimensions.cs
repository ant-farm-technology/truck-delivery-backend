using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanTilt",
                schema: "order",
                table: "order_items",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightM",
                schema: "order",
                table: "order_items",
                type: "decimal(8,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LengthM",
                schema: "order",
                table: "order_items",
                type: "decimal(8,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WidthM",
                schema: "order",
                table: "order_items",
                type: "decimal(8,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanTilt",
                schema: "order",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "HeightM",
                schema: "order",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "LengthM",
                schema: "order",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "WidthM",
                schema: "order",
                table: "order_items");
        }
    }
}
