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
                name: "FK_StockPrices_StockDetails_StockIsin",
                table: "StockPrices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StockDetails",
                table: "StockDetails");

            migrationBuilder.AlterColumn<string>(
                name: "Isin",
                table: "StockDetails",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockDetails",
                table: "StockDetails",
                column: "Isin");

            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Ticker",
                table: "StockDetails",
                column: "Ticker");

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

            migrationBuilder.DropPrimaryKey(
                name: "PK_StockDetails",
                table: "StockDetails");

            migrationBuilder.DropIndex(
                name: "IX_StockDetails_Ticker",
                table: "StockDetails");

            migrationBuilder.AlterColumn<string>(
                name: "Isin",
                table: "StockDetails",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockDetails",
                table: "StockDetails",
                column: "Ticker");

            migrationBuilder.AddForeignKey(
                name: "FK_StockPrices_StockDetails_StockIsin",
                table: "StockPrices",
                column: "StockIsin",
                principalTable: "StockDetails",
                principalColumn: "Ticker",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
