using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class SwitchStockDetailsPrimaryKeyToIsin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockPrices_StockDetails_StockTicker",
                table: "StockPrices");

            // Backfill placeholder ISINs for rows added before ticker→ISIN resolution existed.
            // Ticker was the previous PK (so values are unique) and ≤32 chars; first 12 chars
            // remain unique per row count in dev, satisfying the new PK+NOT NULL on Isin.
            migrationBuilder.Sql(@"
                UPDATE ""StockDetails""
                SET ""Isin"" = LEFT(""Ticker"", 12)
                WHERE ""Isin"" IS NULL OR ""Isin"" = '';
            ");

            migrationBuilder.DropIndex(
                name: "IX_StockDetails_Isin",
                table: "StockDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StockDetails",
                table: "StockDetails");

            migrationBuilder.AlterColumn<string>(
                name: "Isin",
                table: "StockDetails",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockDetails",
                table: "StockDetails",
                column: "Isin");

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Ticker",
                table: "StockDetails",
                column: "Ticker");

            migrationBuilder.AddColumn<string>(
                name: "StockIsin",
                table: "StockPrices",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE ""StockPrices"" sp
                SET ""StockIsin"" = sd.""Isin""
                FROM ""StockDetails"" sd
                WHERE sp.""StockTicker"" = sd.""Ticker"";
            ");

            migrationBuilder.DropIndex(
                name: "IX_StockPrices_StockTicker_Date",
                table: "StockPrices");

            migrationBuilder.DropColumn(
                name: "StockTicker",
                table: "StockPrices");

            migrationBuilder.CreateIndex(
                name: "IX_StockPrices_StockIsin_Date",
                table: "StockPrices",
                columns: new[] { "StockIsin", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_StockPrices_StockDetails_StockIsin",
                table: "StockPrices",
                column: "StockIsin",
                principalTable: "StockDetails",
                principalColumn: "Isin",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockPrices_StockDetails_StockIsin",
                table: "StockPrices");

            migrationBuilder.DropIndex(
                name: "IX_StockPrices_StockIsin_Date",
                table: "StockPrices");

            migrationBuilder.AddColumn<string>(
                name: "StockTicker",
                table: "StockPrices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE ""StockPrices"" sp
                SET ""StockTicker"" = sd.""Ticker""
                FROM ""StockDetails"" sd
                WHERE sp.""StockIsin"" = sd.""Isin"";
            ");

            migrationBuilder.DropColumn(
                name: "StockIsin",
                table: "StockPrices");

            migrationBuilder.CreateIndex(
                name: "IX_StockPrices_StockTicker_Date",
                table: "StockPrices",
                columns: new[] { "StockTicker", "Date" });

            migrationBuilder.DropIndex(
                name: "IX_StockDetails_Ticker",
                table: "StockDetails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StockDetails",
                table: "StockDetails");

            migrationBuilder.AlterColumn<string>(
                name: "Isin",
                table: "StockDetails",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockDetails",
                table: "StockDetails",
                column: "Ticker");

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Isin",
                table: "StockDetails",
                column: "Isin");

            migrationBuilder.AddForeignKey(
                name: "FK_StockPrices_StockDetails_StockTicker",
                table: "StockPrices",
                column: "StockTicker",
                principalTable: "StockDetails",
                principalColumn: "Ticker",
                onDelete: ReferentialAction.Cascade);
        }
    }
}