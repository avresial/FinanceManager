using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class stockPriceUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FinancialAccountBaseDto",
                table: "FinancialAccountBaseDto");

            migrationBuilder.RenameTable(
                name: "FinancialAccountBaseDto",
                newName: "Accounts");

            migrationBuilder.AddColumn<bool>(
                name: "Verified",
                table: "StockPrices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Accounts",
                table: "Accounts",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Accounts",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Verified",
                table: "StockPrices");

            migrationBuilder.RenameTable(
                name: "Accounts",
                newName: "FinancialAccountBaseDto");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FinancialAccountBaseDto",
                table: "FinancialAccountBaseDto",
                column: "AccountId");
        }
    }
}
