using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerTransactionHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("""
                    DELETE FROM "InvestmentTransactions" AS e
                    WHERE e."AccountId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "Accounts" AS a WHERE a."AccountId" = e."AccountId" AND a."AccountType" = 1);
                    DELETE FROM "DividendPayments" AS e
                    WHERE e."AccountId" IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM "Accounts" AS a WHERE a."AccountId" = e."AccountId" AND a."AccountType" = 1);
                    """);
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
                migrationBuilder.Sql("""
                    DELETE e FROM [InvestmentTransactions] AS e
                    WHERE e.[AccountId] IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM [Accounts] AS a WHERE a.[AccountId] = e.[AccountId] AND a.[AccountType] = 1);
                    DELETE e FROM [DividendPayments] AS e
                    WHERE e.[AccountId] IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM [Accounts] AS a WHERE a.[AccountId] = e.[AccountId] AND a.[AccountType] = 1);
                    """);

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyEntries_OwnerDateId",
                table: "CurrencyEntries",
                columns: new[] { "UserId", "PostingDate", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_BondEntries_OwnerDateId",
                table: "BondEntries",
                columns: new[] { "UserId", "PostingDate", "EntryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurrencyEntries_OwnerDateId",
                table: "CurrencyEntries");

            migrationBuilder.DropIndex(
                name: "IX_BondEntries_OwnerDateId",
                table: "BondEntries");
        }
    }
}