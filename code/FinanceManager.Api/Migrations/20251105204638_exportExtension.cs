using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class exportExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DuplicateEntries");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "StockPrices");

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "StockPrices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Currency",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShortName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currency", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockPrices_CurrencyId",
                table: "StockPrices",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockPrices_Currency_CurrencyId",
                table: "StockPrices",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockPrices_Currency_CurrencyId",
                table: "StockPrices");

            migrationBuilder.DropTable(
                name: "Currency");

            migrationBuilder.DropIndex(
                name: "IX_StockPrices_CurrencyId",
                table: "StockPrices");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "StockPrices");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "StockPrices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DuplicateEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    EntriesId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateEntries", x => x.Id);
                });
        }
    }
}
