using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VShop.DiscountApi.Migrations
{
    /// <inheritdoc />
    public partial class CouponReservationDiscountSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "CouponReservations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "CouponReservations");
        }
    }
}
