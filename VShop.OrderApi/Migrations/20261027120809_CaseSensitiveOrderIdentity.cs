using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VShop.OrderApi.Migrations
{
    /// <inheritdoc />
    public partial class CaseSensitiveOrderIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "OrderHeaders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "OrderHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "Latin1_General_100_BIN2",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "OrderHeaders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldCollation: "Latin1_General_100_BIN2");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotencyKey",
                table: "OrderHeaders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldCollation: "Latin1_General_100_BIN2");
        }
    }
}
