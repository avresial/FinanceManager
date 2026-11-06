using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations;

public partial class StoreInvestmentPostingDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => ConvertDate(migrationBuilder, false);
    protected override void Down(MigrationBuilder migrationBuilder) => ConvertDate(migrationBuilder, true);

    private void ConvertDate(MigrationBuilder migrationBuilder, bool down)
    {
        var oldColumn = down ? "PostingDate" : "TradeDate";
        var newColumn = down ? "TradeDate" : "PostingDate";
        foreach (var prefix in new[] { "AccountId", "UserId" })
            migrationBuilder.DropIndex($"IX_InvestmentTransactions_{prefix}_{oldColumn}" + (prefix == "AccountId" ? "_Id" : ""), "InvestmentTransactions");
        if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            // Explicit UTC conversions preserve calendar dates regardless of the session time zone.
            var conversion = down ? """("PostingDate" AT TIME ZONE 'UTC')::date""" : """("TradeDate"::timestamp AT TIME ZONE 'UTC')""";
            migrationBuilder.Sql($"""
                ALTER TABLE "InvestmentTransactions" ALTER COLUMN "{oldColumn}" TYPE {(down ? "date" : "timestamp with time zone")} USING ({conversion});
                ALTER TABLE "InvestmentTransactions" RENAME COLUMN "{oldColumn}" TO "{newColumn}";
                """);
        }
        else if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            migrationBuilder.Sql($"""
                ALTER TABLE [InvestmentTransactions] ALTER COLUMN [{oldColumn}] {(down ? "date" : "datetime2")} NOT NULL;
                EXEC sp_rename N'InvestmentTransactions.{oldColumn}', N'{newColumn}', N'COLUMN';
                """);
        else
            throw new NotSupportedException($"Unsupported migration provider: {ActiveProvider}");
        migrationBuilder.CreateIndex($"IX_InvestmentTransactions_AccountId_{newColumn}_Id", "InvestmentTransactions", new[] { "AccountId", newColumn, "Id" });
        migrationBuilder.CreateIndex($"IX_InvestmentTransactions_UserId_{newColumn}", "InvestmentTransactions", new[] { "UserId", newColumn });
    }
}