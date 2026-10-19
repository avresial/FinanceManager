using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentTransactionHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactions_AccountId_TradeDate_Id",
                table: "InvestmentTransactions",
                columns: new[] { "AccountId", "TradeDate", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvestmentTransactions_AccountId_TradeDate_Id",
                table: "InvestmentTransactions");
        }
    }
}