using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringSubscriptionReferenceAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurringSubscriptions_UserId_MerchantKey",
                table: "RecurringSubscriptions");

            migrationBuilder.AddColumn<decimal>(
                name: "ReferenceAmount",
                table: "RecurringSubscriptions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringSubscriptions_UserId_MerchantKey_ReferenceAmount",
                table: "RecurringSubscriptions",
                columns: new[] { "UserId", "MerchantKey", "ReferenceAmount" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurringSubscriptions_UserId_MerchantKey_ReferenceAmount",
                table: "RecurringSubscriptions");

            migrationBuilder.DropColumn(
                name: "ReferenceAmount",
                table: "RecurringSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringSubscriptions_UserId_MerchantKey",
                table: "RecurringSubscriptions",
                columns: new[] { "UserId", "MerchantKey" },
                unique: true);
        }
    }
}