using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentTransactionLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvestmentTransactionFinancialLabel",
                columns: table => new
                {
                    InvestmentTransactionEntryId = table.Column<long>(type: "bigint", nullable: false),
                    LabelsId = table.Column<int>(type: ActiveProvider.Contains("SqlServer") ? "int" : "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentTransactionFinancialLabel", x => new { x.InvestmentTransactionEntryId, x.LabelsId });
                    table.ForeignKey(
                        name: "FK_InvestmentTransactionFinancialLabel_FinancialLabels_LabelsId",
                        column: x => x.LabelsId,
                        principalTable: "FinancialLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InvestmentTransactionFinancialLabel_InvestmentTransactions_~",
                        column: x => x.InvestmentTransactionEntryId,
                        principalTable: "InvestmentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentTransactionFinancialLabel_LabelsId",
                table: "InvestmentTransactionFinancialLabel",
                column: "LabelsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvestmentTransactionFinancialLabel");
        }
    }
}