using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class StoreInvestmentRunningUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Value",
                table: "InvestmentTransactions",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            // Backfill the running units per account and listing. InvestmentTransactionType is stored as an int:
            // Buy = 0 adds the quantity, Sell = 1 subtracts it. Oversells keep their negative holding.
            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
                migrationBuilder.Sql("""
                    UPDATE "InvestmentTransactions" AS t
                    SET "Value" = r."Running"
                    FROM (
                        SELECT "Id",
                               SUM(CASE "Type" WHEN 0 THEN "Quantity" WHEN 1 THEN -"Quantity" ELSE 0 END) OVER (
                                   PARTITION BY "AccountId", "AssetListingId"
                                   ORDER BY "PostingDate", "Id"
                                   ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS "Running"
                        FROM "InvestmentTransactions"
                    ) AS r
                    WHERE t."Id" = r."Id";
                    """);
            else if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
                migrationBuilder.Sql("""
                    UPDATE t
                    SET t.[Value] = r.[Running]
                    FROM [InvestmentTransactions] AS t
                    INNER JOIN (
                        SELECT [Id],
                               SUM(CASE [Type] WHEN 0 THEN [Quantity] WHEN 1 THEN -[Quantity] ELSE 0 END) OVER (
                                   PARTITION BY [AccountId], [AssetListingId]
                                   ORDER BY [PostingDate], [Id]
                                   ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS [Running]
                        FROM [InvestmentTransactions]
                    ) AS r ON r.[Id] = t.[Id];
                    """);
            else
                throw new NotSupportedException($"Unsupported migration provider: {ActiveProvider}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Value",
                table: "InvestmentTransactions");
        }
    }
}