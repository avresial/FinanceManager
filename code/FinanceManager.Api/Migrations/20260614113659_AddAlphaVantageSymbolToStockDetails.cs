using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAlphaVantageSymbolToStockDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlphaVantageSymbol",
                table: "StockDetails",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlphaVantageSymbol",
                table: "StockDetails");
        }
    }
}