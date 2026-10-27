using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VShop.DiscountApi.Migrations
{
    /// <inheritdoc />
    public partial class SagaManagedCouponReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ManagedBySaga",
                table: "CouponReservations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagedBySaga",
                table: "CouponReservations");
        }
    }
}
