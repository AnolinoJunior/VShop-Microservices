using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vshop.ProductApi.Migrations
{
    /// <inheritdoc />
    public partial class SagaManagedStockReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ManagedBySaga",
                table: "StockReservations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagedBySaga",
                table: "StockReservations");
        }
    }
}
