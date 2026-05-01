using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Payment.Infrastructure.Migrations
{
    public partial class AddPaymentMethodToPayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "Payments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Cod");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Method", table: "Payments");
        }
    }
}
