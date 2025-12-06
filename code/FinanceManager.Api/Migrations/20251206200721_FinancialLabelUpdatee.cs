using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class FinancialLabelUpdatee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccountEntryFinancialLabel_BankEntries_EntriesEntryId",
                table: "BankAccountEntryFinancialLabel");

            migrationBuilder.RenameColumn(
                name: "EntriesEntryId",
                table: "BankAccountEntryFinancialLabel",
                newName: "BankAccountEntryEntryId");

            migrationBuilder.AddColumn<int>(
                name: "BondAccountEntryEntryId",
                table: "FinancialLabels",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Bonds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Issuer",
                table: "Bonds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "Bonds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BondCalculationMethod",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BondDetailsId = table.Column<int>(type: "int", nullable: false),
                    DateOperator = table.Column<int>(type: "int", nullable: false),
                    DateValue = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondCalculationMethod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BondCalculationMethod_Bonds_BondDetailsId",
                        column: x => x.BondDetailsId,
                        principalTable: "Bonds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BondEntries",
                columns: table => new
                {
                    EntryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BondDetailsId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueChange = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondEntries", x => x.EntryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialLabels_BondAccountEntryEntryId",
                table: "FinancialLabels",
                column: "BondAccountEntryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Bonds_CurrencyId",
                table: "Bonds",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_BondCalculationMethod_BondDetailsId",
                table: "BondCalculationMethod",
                column: "BondDetailsId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccountEntryFinancialLabel_BankEntries_BankAccountEntryEntryId",
                table: "BankAccountEntryFinancialLabel",
                column: "BankAccountEntryEntryId",
                principalTable: "BankEntries",
                principalColumn: "EntryId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bonds_Currency_CurrencyId",
                table: "Bonds",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialLabels_BondEntries_BondAccountEntryEntryId",
                table: "FinancialLabels",
                column: "BondAccountEntryEntryId",
                principalTable: "BondEntries",
                principalColumn: "EntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccountEntryFinancialLabel_BankEntries_BankAccountEntryEntryId",
                table: "BankAccountEntryFinancialLabel");

            migrationBuilder.DropForeignKey(
                name: "FK_Bonds_Currency_CurrencyId",
                table: "Bonds");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialLabels_BondEntries_BondAccountEntryEntryId",
                table: "FinancialLabels");

            migrationBuilder.DropTable(
                name: "BondCalculationMethod");

            migrationBuilder.DropTable(
                name: "BondEntries");

            migrationBuilder.DropIndex(
                name: "IX_FinancialLabels_BondAccountEntryEntryId",
                table: "FinancialLabels");

            migrationBuilder.DropIndex(
                name: "IX_Bonds_CurrencyId",
                table: "Bonds");

            migrationBuilder.DropColumn(
                name: "BondAccountEntryEntryId",
                table: "FinancialLabels");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Bonds");

            migrationBuilder.RenameColumn(
                name: "BankAccountEntryEntryId",
                table: "BankAccountEntryFinancialLabel",
                newName: "EntriesEntryId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Bonds",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Issuer",
                table: "Bonds",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccountEntryFinancialLabel_BankEntries_EntriesEntryId",
                table: "BankAccountEntryFinancialLabel",
                column: "EntriesEntryId",
                principalTable: "BankEntries",
                principalColumn: "EntryId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
