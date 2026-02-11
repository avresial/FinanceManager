using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddedStockDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bonds_Currency_CurrencyId",
                table: "Bonds");

            migrationBuilder.DropForeignKey(
                name: "FK_StockPrices_Currency_CurrencyId",
                table: "StockPrices");

            migrationBuilder.DropIndex(
                name: "IX_StockPrices_CurrencyId",
                table: "StockPrices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Currency",
                table: "Currency");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "StockPrices");

            migrationBuilder.DropColumn(
                name: "Ticker",
                table: "StockPrices");

            migrationBuilder.DropColumn(
                name: "Verified",
                table: "StockPrices");

            migrationBuilder.RenameTable(
                name: "Currency",
                newName: "Currencies");

            migrationBuilder.AddColumn<string>(
                name: "StockTicker",
                table: "StockPrices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Symbol",
                table: "Currencies",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ShortName",
                table: "Currencies",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Currencies",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Currencies",
                table: "Currencies",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "StockDetails",
                columns: table => new
                {
                    Ticker = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Region = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CurrencyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockDetails", x => x.Ticker);
                    table.ForeignKey(
                        name: "FK_StockDetails_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockPrices_StockTicker",
                table: "StockPrices",
                column: "StockTicker");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_ShortName",
                table: "Currencies",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_CurrencyId",
                table: "StockDetails",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bonds_Currencies_CurrencyId",
                table: "Bonds",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StockPrices_StockDetails_StockTicker",
                table: "StockPrices",
                column: "StockTicker",
                principalTable: "StockDetails",
                principalColumn: "Ticker",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bonds_Currencies_CurrencyId",
                table: "Bonds");

            migrationBuilder.DropForeignKey(
                name: "FK_StockPrices_StockDetails_StockTicker",
                table: "StockPrices");

            migrationBuilder.DropTable(
                name: "StockDetails");

            migrationBuilder.DropIndex(
                name: "IX_StockPrices_StockTicker",
                table: "StockPrices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Currencies",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_ShortName",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "StockTicker",
                table: "StockPrices");

            migrationBuilder.RenameTable(
                name: "Currencies",
                newName: "Currency");

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "StockPrices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ticker",
                table: "StockPrices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Verified",
                table: "StockPrices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Symbol",
                table: "Currency",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);

            migrationBuilder.AlterColumn<string>(
                name: "ShortName",
                table: "Currency",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Currency",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Currency",
                table: "Currency",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_StockPrices_CurrencyId",
                table: "StockPrices",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bonds_Currency_CurrencyId",
                table: "Bonds",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StockPrices_Currency_CurrencyId",
                table: "StockPrices",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id");
        }
    }
}
