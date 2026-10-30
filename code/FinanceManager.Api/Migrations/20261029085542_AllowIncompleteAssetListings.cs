using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowIncompleteAssetListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sqlServer = ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer";

            migrationBuilder.DropIndex(
                name: "IX_AssetListings_Ticker_ExchangeMic_TradingCurrency",
                table: "AssetListings");

            if (sqlServer)
            {
                migrationBuilder.DropIndex(
                    name: "IX_AssetListings_ExchangeMic",
                    table: "AssetListings");
            }

            migrationBuilder.AlterColumn<string>(
                name: "TradingCurrency",
                table: "AssetListings",
                type: sqlServer ? "nvarchar(3)" : "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: sqlServer ? "nvarchar(3)" : "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "ExchangeMic",
                table: "AssetListings",
                type: sqlServer ? "nvarchar(16)" : "character varying(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: sqlServer ? "nvarchar(16)" : "character varying(16)",
                oldMaxLength: 16);

            if (sqlServer)
            {
                migrationBuilder.CreateIndex(
                    name: "IX_AssetListings_ExchangeMic",
                    table: "AssetListings",
                    column: "ExchangeMic");
            }

            migrationBuilder.CreateIndex(
                name: "IX_AssetListings_Ticker_ExchangeMic_TradingCurrency",
                table: "AssetListings",
                columns: new[] { "Ticker", "ExchangeMic", "TradingCurrency" },
                unique: true,
                filter: sqlServer
                    ? "[ExchangeMic] IS NOT NULL AND [TradingCurrency] IS NOT NULL"
                    : "\"ExchangeMic\" IS NOT NULL AND \"TradingCurrency\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var sqlServer = ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer";

            migrationBuilder.DropIndex(
                name: "IX_AssetListings_Ticker_ExchangeMic_TradingCurrency",
                table: "AssetListings");

            if (sqlServer)
            {
                migrationBuilder.DropIndex(
                    name: "IX_AssetListings_ExchangeMic",
                    table: "AssetListings");

                migrationBuilder.Sql("UPDATE [AssetListings] SET [TradingCurrency] = N'' WHERE [TradingCurrency] IS NULL;");
                migrationBuilder.Sql("UPDATE [AssetListings] SET [ExchangeMic] = N'' WHERE [ExchangeMic] IS NULL;");
            }
            else
            {
                migrationBuilder.Sql("UPDATE \"AssetListings\" SET \"TradingCurrency\" = '' WHERE \"TradingCurrency\" IS NULL;");
                migrationBuilder.Sql("UPDATE \"AssetListings\" SET \"ExchangeMic\" = '' WHERE \"ExchangeMic\" IS NULL;");
            }

            migrationBuilder.AlterColumn<string>(
                name: "TradingCurrency",
                table: "AssetListings",
                type: sqlServer ? "nvarchar(3)" : "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: sqlServer ? "nvarchar(3)" : "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExchangeMic",
                table: "AssetListings",
                type: sqlServer ? "nvarchar(16)" : "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: sqlServer ? "nvarchar(16)" : "character varying(16)",
                oldMaxLength: 16,
                oldNullable: true);

            if (sqlServer)
            {
                migrationBuilder.CreateIndex(
                    name: "IX_AssetListings_ExchangeMic",
                    table: "AssetListings",
                    column: "ExchangeMic");
            }

            migrationBuilder.CreateIndex(
                name: "IX_AssetListings_Ticker_ExchangeMic_TradingCurrency",
                table: "AssetListings",
                columns: new[] { "Ticker", "ExchangeMic", "TradingCurrency" },
                unique: true);
        }
    }
}