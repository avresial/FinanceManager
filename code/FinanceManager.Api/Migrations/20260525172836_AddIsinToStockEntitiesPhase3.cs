using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsinToStockEntitiesPhase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 3a: Add nullable ISIN columns for backfill
            migrationBuilder.AddColumn<string>(
                name: "Isin",
                table: "StockDetails",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Isin",
                table: "StockEntries",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Ticker",
                table: "StockEntries",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // Create index for faster lookups during backfill
            migrationBuilder.CreateIndex(
                name: "IX_StockDetails_Isin",
                table: "StockDetails",
                column: "Isin",
                unique: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockDetails_Isin",
                table: "StockDetails");

            migrationBuilder.DropColumn(
                name: "Isin",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "Isin",
                table: "StockDetails");

            migrationBuilder.AlterColumn<string>(
                name: "Ticker",
                table: "StockEntries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);
        }
    }
}