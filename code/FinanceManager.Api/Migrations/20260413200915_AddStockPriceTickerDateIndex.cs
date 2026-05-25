using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStockPriceTickerDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockPrices_StockTicker",
                table: "StockPrices");

            migrationBuilder.CreateIndex(
                name: "IX_StockPrices_StockTicker_Date",
                table: "StockPrices",
                columns: new[] { "StockTicker", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockPrices_StockTicker_Date",
                table: "StockPrices");

            migrationBuilder.CreateIndex(
                name: "IX_StockPrices_StockTicker",
                table: "StockPrices",
                column: "StockTicker");
        }
    }
}