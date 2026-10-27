using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VShop.OrderApi.Migrations
{
    /// <inheritdoc />
    public partial class CheckoutSagaState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckoutSagas",
                columns: table => new
                {
                    OrderHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SimulateDecline = table.Column<bool>(type: "bit", nullable: false),
                    CouponReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CouponReserved = table.Column<bool>(type: "bit", nullable: false),
                    CouponPrepared = table.Column<bool>(type: "bit", nullable: false),
                    CouponConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    CouponReleased = table.Column<bool>(type: "bit", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutSagas", x => x.OrderHeaderId);
                    table.ForeignKey(
                        name: "FK_CheckoutSagas_OrderHeaders_OrderHeaderId",
                        column: x => x.OrderHeaderId,
                        principalTable: "OrderHeaders",
                        principalColumn: "OrderHeaderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CheckoutSagaItems",
                columns: table => new
                {
                    OrderDetailId = table.Column<long>(type: "bigint", nullable: false),
                    OrderHeaderId = table.Column<long>(type: "bigint", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reserved = table.Column<bool>(type: "bit", nullable: false),
                    Prepared = table.Column<bool>(type: "bit", nullable: false),
                    Confirmed = table.Column<bool>(type: "bit", nullable: false),
                    Released = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutSagaItems", x => x.OrderDetailId);
                    table.ForeignKey(
                        name: "FK_CheckoutSagaItems_CheckoutSagas_OrderHeaderId",
                        column: x => x.OrderHeaderId,
                        principalTable: "CheckoutSagas",
                        principalColumn: "OrderHeaderId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CheckoutSagaItems_OrderDetails_OrderDetailId",
                        column: x => x.OrderDetailId,
                        principalTable: "OrderDetails",
                        principalColumn: "OrderDetailId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSagaItems_OrderHeaderId",
                table: "CheckoutSagaItems",
                column: "OrderHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckoutSagaItems_ReservationId",
                table: "CheckoutSagaItems",
                column: "ReservationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckoutSagaItems");

            migrationBuilder.DropTable(
                name: "CheckoutSagas");
        }
    }
}
