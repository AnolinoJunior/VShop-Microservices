using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VShop.OrderApi.Migrations
{
    /// <inheritdoc />
    public partial class PersistSagaCommitDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CommitDecision",
                table: "CheckoutSagas",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommitDecision",
                table: "CheckoutSagas");
        }
    }
}
