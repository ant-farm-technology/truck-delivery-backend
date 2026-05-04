using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TruckDelivery.Driver.Infrastructure.Migrations
{
    public partial class AddBreakdownAndSwapRecords : Migration
    {
        // All DDL for this migration was consolidated into InitialCreate.
        // This class exists only so __EFMigrationsHistory rows from older deployments
        // remain valid and EF does not attempt to re-apply a "missing" migration.
        protected override void Up(MigrationBuilder migrationBuilder) { }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
