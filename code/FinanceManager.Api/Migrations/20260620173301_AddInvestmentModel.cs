using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Isin = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    ShareClassFigi = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CompositeFigi = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Issuer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Domicile = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    DistributionPolicy = table.Column<int>(type: "integer", nullable: true),
                    BenchmarkIndex = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReplicationMethod = table.Column<int>(type: "integer", nullable: true),
                    TotalExpenseRatio = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    IsUcits = table.Column<bool>(type: "boolean", nullable: true),
                    InceptionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssetIdentifiers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetIdentifiers_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssetListings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetId = table.Column<long>(type: "bigint", nullable: false),
                    Ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExchangeMic = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExchangeName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TradingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ListingFigi = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ExchangeInstrumentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsPrimaryListing = table.Column<bool>(type: "boolean", nullable: false),
                    PriceMultiplier = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetListings_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    AssetListingId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestmentTransactions_AssetListings_AssetListingId",
                        column: x => x.AssetListingId,
                        principalTable: "AssetListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataSymbols",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetListingId = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderExchangeCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProviderInstrumentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastValidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSuccessfulPriceFetchAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataSymbols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketDataSymbols_AssetListings_AssetListingId",
                        column: x => x.AssetListingId,
                        principalTable: "AssetListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceQuotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssetListingId = table.Column<long>(type: "bigint", nullable: false),
                    MarketDataSymbolId = table.Column<long>(type: "bigint", nullable: true),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PriceTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    QuoteType = table.Column<int>(type: "integer", nullable: false),
                    RawPrice = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    RawCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceQuotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceQuotes_AssetListings_AssetListingId",
                        column: x => x.AssetListingId,
                        principalTable: "AssetListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceQuotes_MarketDataSymbols_MarketDataSymbolId",
                        column: x => x.MarketDataSymbolId,
                        principalTable: "MarketDataSymbols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetIdentifiers_AssetId",
                table: "AssetIdentifiers",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetIdentifiers_Type",
                table: "AssetIdentifiers",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AssetIdentifiers_Type_Value",
                table: "AssetIdentifiers",
                columns: new[] { "Type", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetIdentifiers_Value",
                table: "AssetIdentifiers",
                column: "Value");

            migrationBuilder.CreateIndex(
                name: "IX_AssetListings_AssetId",
                table: "AssetListings",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetListings_ExchangeMic",
                table: "AssetListings",
                column: "ExchangeMic");

            migrationBuilder.CreateIndex(
                name: "IX_AssetListings_ListingFigi",
                table: "AssetListings",
                column: "ListingFigi");

            migrationBuilder.CreateIndex(
                name: "IX_AssetListings_Ticker",
                table: "AssetListings",
                column: "Ticker");

            migrationBuilder.CreateIndex(
                name: "IX_AssetListings_Ticker_ExchangeMic_TradingCurrency",
                table: "AssetListings",
                columns: new[] { "Ticker", "ExchangeMic", "TradingCurrency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Isin",
                table: "Assets",
                column: "Isin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Name",
                table: "Assets",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Type",
                table: "Assets",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactions_AccountId",
                table: "InvestmentTransactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactions_AssetListingId",
                table: "InvestmentTransactions",
                column: "AssetListingId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactions_UserId",
                table: "InvestmentTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactions_UserId_AssetListingId",
                table: "InvestmentTransactions",
                columns: new[] { "UserId", "AssetListingId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactions_UserId_TradeDate",
                table: "InvestmentTransactions",
                columns: new[] { "UserId", "TradeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataSymbols_AssetListingId",
                table: "MarketDataSymbols",
                column: "AssetListingId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataSymbols_AssetListingId_Provider_Symbol",
                table: "MarketDataSymbols",
                columns: new[] { "AssetListingId", "Provider", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataSymbols_IsEnabled",
                table: "MarketDataSymbols",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataSymbols_Provider",
                table: "MarketDataSymbols",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataSymbols_Provider_Symbol",
                table: "MarketDataSymbols",
                columns: new[] { "Provider", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceQuotes_AssetListingId_PriceTime",
                table: "PriceQuotes",
                columns: new[] { "AssetListingId", "PriceTime" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceQuotes_AssetListingId_Provider_PriceTime_QuoteType",
                table: "PriceQuotes",
                columns: new[] { "AssetListingId", "Provider", "PriceTime", "QuoteType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceQuotes_MarketDataSymbolId",
                table: "PriceQuotes",
                column: "MarketDataSymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceQuotes_Provider",
                table: "PriceQuotes",
                column: "Provider");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetIdentifiers");

            migrationBuilder.DropTable(
                name: "InvestmentTransactions");

            migrationBuilder.DropTable(
                name: "PriceQuotes");

            migrationBuilder.DropTable(
                name: "MarketDataSymbols");

            migrationBuilder.DropTable(
                name: "AssetListings");

            migrationBuilder.DropTable(
                name: "Assets");
        }
    }
}