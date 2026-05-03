using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Payment.Infrastructure.Migrations
{
    public partial class AddDriverIdToPayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DriverId",
                table: "Payments",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DriverId",
                table: "Payments",
                column: "DriverId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Payments_DriverId", table: "Payments");
            migrationBuilder.DropColumn(name: "DriverId", table: "Payments");
        }
    }
}
