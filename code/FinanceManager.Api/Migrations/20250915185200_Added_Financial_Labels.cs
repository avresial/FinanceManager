using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class Added_Financial_Labels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpenseType",
                table: "BankEntries");

            migrationBuilder.CreateTable(
                name: "FinancialLabels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StockAccountEntryEntryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialLabels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialLabels_StockEntries_StockAccountEntryEntryId",
                        column: x => x.StockAccountEntryEntryId,
                        principalTable: "StockEntries",
                        principalColumn: "EntryId");
                });

            migrationBuilder.CreateTable(
                name: "BankAccountEntryFinancialLabel",
                columns: table => new
                {
                    EntriesEntryId = table.Column<int>(type: "int", nullable: false),
                    LabelsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccountEntryFinancialLabel", x => new { x.EntriesEntryId, x.LabelsId });
                    table.ForeignKey(
                        name: "FK_BankAccountEntryFinancialLabel_BankEntries_EntriesEntryId",
                        column: x => x.EntriesEntryId,
                        principalTable: "BankEntries",
                        principalColumn: "EntryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankAccountEntryFinancialLabel_FinancialLabels_LabelsId",
                        column: x => x.LabelsId,
                        principalTable: "FinancialLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccountEntryFinancialLabel_LabelsId",
                table: "BankAccountEntryFinancialLabel",
                column: "LabelsId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLabels_StockAccountEntryEntryId",
                table: "FinancialLabels",
                column: "StockAccountEntryEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankAccountEntryFinancialLabel");

            migrationBuilder.DropTable(
                name: "FinancialLabels");

            migrationBuilder.AddColumn<int>(
                name: "ExpenseType",
                table: "BankEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}