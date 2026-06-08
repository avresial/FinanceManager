using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHotQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockEntries_AccountId_Isin_PostingDate_EntryId",
                table: "StockEntries",
                columns: new[] { "AccountId", "Isin", "PostingDate", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockEntries_AccountId_PostingDate_EntryId",
                table: "StockEntries",
                columns: new[] { "AccountId", "PostingDate", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyEntries_AccountId_PostingDate_EntryId",
                table: "CurrencyEntries",
                columns: new[] { "AccountId", "PostingDate", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_BondEntries_AccountId_BondDetailsId_PostingDate_EntryId",
                table: "BondEntries",
                columns: new[] { "AccountId", "BondDetailsId", "PostingDate", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_BondEntries_AccountId_PostingDate_EntryId",
                table: "BondEntries",
                columns: new[] { "AccountId", "PostingDate", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountType_UserId",
                table: "Accounts",
                columns: new[] { "AccountType", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockEntries_AccountId_Isin_PostingDate_EntryId",
                table: "StockEntries");

            migrationBuilder.DropIndex(
                name: "IX_StockEntries_AccountId_PostingDate_EntryId",
                table: "StockEntries");

            migrationBuilder.DropIndex(
                name: "IX_CurrencyEntries_AccountId_PostingDate_EntryId",
                table: "CurrencyEntries");

            migrationBuilder.DropIndex(
                name: "IX_BondEntries_AccountId_BondDetailsId_PostingDate_EntryId",
                table: "BondEntries");

            migrationBuilder.DropIndex(
                name: "IX_BondEntries_AccountId_PostingDate_EntryId",
                table: "BondEntries");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_AccountType_UserId",
                table: "Accounts");
        }
    }
}
