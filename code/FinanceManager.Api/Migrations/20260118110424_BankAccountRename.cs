using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using System;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class BankAccountRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankAccountEntryFinancialLabel");

            migrationBuilder.DropTable(
                name: "BankEntries");

            migrationBuilder.CreateTable(
                name: "CurrencyEntries",
                columns: table => new
                {
                    EntryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueChange = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyEntries", x => x.EntryId);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyAccountEntryFinancialLabel",
                columns: table => new
                {
                    CurrencyAccountEntryEntryId = table.Column<int>(type: "integer", nullable: false),
                    LabelsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyAccountEntryFinancialLabel", x => new { x.CurrencyAccountEntryEntryId, x.LabelsId });
                    table.ForeignKey(
                        name: "FK_CurrencyAccountEntryFinancialLabel_CurrencyEntries_Currency~",
                        column: x => x.CurrencyAccountEntryEntryId,
                        principalTable: "CurrencyEntries",
                        principalColumn: "EntryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CurrencyAccountEntryFinancialLabel_FinancialLabels_LabelsId",
                        column: x => x.LabelsId,
                        principalTable: "FinancialLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyAccountEntryFinancialLabel_LabelsId",
                table: "CurrencyAccountEntryFinancialLabel",
                column: "LabelsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurrencyAccountEntryFinancialLabel");

            migrationBuilder.DropTable(
                name: "CurrencyEntries");

            migrationBuilder.CreateTable(
                name: "BankEntries",
                columns: table => new
                {
                    EntryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    PostingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValueChange = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankEntries", x => x.EntryId);
                });

            migrationBuilder.CreateTable(
                name: "BankAccountEntryFinancialLabel",
                columns: table => new
                {
                    BankAccountEntryEntryId = table.Column<int>(type: "integer", nullable: false),
                    LabelsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccountEntryFinancialLabel", x => new { x.BankAccountEntryEntryId, x.LabelsId });
                    table.ForeignKey(
                        name: "FK_BankAccountEntryFinancialLabel_BankEntries_BankAccountEntry~",
                        column: x => x.BankAccountEntryEntryId,
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
        }
    }
}