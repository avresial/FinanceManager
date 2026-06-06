using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class BackfillStockEntriesIsin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill StockEntries.Isin from StockDetails by joining on Ticker.
            // Phase 3a added Isin as nullable; the model expects it required, but no
            // follow-up migration backfilled or tightened it for StockEntries.
            migrationBuilder.Sql(@"
                UPDATE ""StockEntries"" se
                SET ""Isin"" = sd.""Isin""
                FROM ""StockDetails"" sd
                WHERE se.""Ticker"" = sd.""Ticker""
                  AND (se.""Isin"" IS NULL OR se.""Isin"" = '');
            ");

            // Fallback for any rows that still have no Isin (no matching StockDetails row):
            // use LEFT(Ticker, 12), mirroring the placeholder approach used in
            // SwitchStockDetailsPrimaryKeyToIsin for StockDetails.
            migrationBuilder.Sql(@"
                UPDATE ""StockEntries""
                SET ""Isin"" = LEFT(""Ticker"", 12)
                WHERE ""Isin"" IS NULL OR ""Isin"" = '';
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Isin",
                table: "StockEntries",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Isin",
                table: "StockEntries",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12);
        }
    }
}