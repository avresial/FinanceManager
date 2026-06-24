using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyStockModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialLabels_StockEntries_StockAccountEntryEntryId",
                table: "FinancialLabels");

            migrationBuilder.DropTable(
                name: "StockEntries");

            migrationBuilder.DropTable(
                name: "StockPrices");

            migrationBuilder.DropTable(
                name: "StockDetails");

            migrationBuilder.DropIndex(
                name: "IX_FinancialLabels_StockAccountEntryEntryId",
                table: "FinancialLabels");

            migrationBuilder.DropColumn(
                name: "StockAccountEntryEntryId",
                table: "FinancialLabels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StockAccountEntryEntryId",
                table: "FinancialLabels",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockDetails",
                columns: table => new
                {
                    Isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CurrencyId = table.Column<int>(type: "integer", nullable: false),
                    AlphaVantageSymbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Region = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockDetails", x => x.Isin);
                    table.ForeignKey(
                        name: "FK_StockDetails_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockEntries",
                columns: table => new
                {
                    EntryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    InvestmentType = table.Column<int>(type: "integer", nullable: false),
                    Isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    ValueChange = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockEntries", x => x.EntryId);
                });

            migrationBuilder.CreateTable(
                name: "StockPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockIsin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PricePerUnit = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockPrices_StockDetails_StockIsin",
                        column: x => x.StockIsin,
                        principalTable: "StockDetails",
                        principalColumn: "Isin",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLabels_StockAccountEntryEntryId",
                table: "FinancialLabels",
                column: "StockAccountEntryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_CurrencyId",
                table: "StockDetails",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Ticker",
                table: "StockDetails",
                column: "Ticker");

            migrationBuilder.CreateIndex(
                name: "IX_StockEntries_AccountId_Isin_PostingDate_EntryId",
                table: "StockEntries",
                columns: new[] { "AccountId", "Isin", "PostingDate", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockEntries_AccountId_PostingDate_EntryId",
                table: "StockEntries",
                columns: new[] { "AccountId", "PostingDate", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockPrices_StockIsin_Date",
                table: "StockPrices",
                columns: new[] { "StockIsin", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialLabels_StockEntries_StockAccountEntryEntryId",
                table: "FinancialLabels",
                column: "StockAccountEntryEntryId",
                principalTable: "StockEntries",
                principalColumn: "EntryId");
        }
    }
}