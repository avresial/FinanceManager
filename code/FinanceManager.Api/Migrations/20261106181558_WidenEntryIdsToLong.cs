using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinanceManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class WidenEntryIdsToLong : Migration
    {
        private const string _currencyLabelForeignKey = "FK_CurrencyAccountEntryFinancialLabel_CurrencyEntries_Currency~";
        private const string _bondLabelForeignKey = "FK_FinancialLabels_BondEntries_BondAccountEntryEntryId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => ChangeEntryIdType<int, long>(migrationBuilder, "integer", "bigint");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => ChangeEntryIdType<long, int>(migrationBuilder, "bigint", "integer");

        // SQL Server cannot alter a column used by a key or index, so both providers drop and recreate them.
        private static void ChangeEntryIdType<TOld, TNew>(MigrationBuilder migrationBuilder, string oldType, string newType)
        {
            migrationBuilder.DropForeignKey(_currencyLabelForeignKey, "CurrencyAccountEntryFinancialLabel");
            migrationBuilder.DropForeignKey(_bondLabelForeignKey, "FinancialLabels");
            migrationBuilder.DropPrimaryKey("PK_CurrencyAccountEntryFinancialLabel", "CurrencyAccountEntryFinancialLabel");
            migrationBuilder.DropPrimaryKey("PK_CurrencyEntries", "CurrencyEntries");
            migrationBuilder.DropPrimaryKey("PK_BondEntries", "BondEntries");
            migrationBuilder.DropIndex("IX_CurrencyEntries_AccountId_PostingDate_EntryId", "CurrencyEntries");
            migrationBuilder.DropIndex("IX_BondEntries_AccountId_PostingDate_EntryId", "BondEntries");
            migrationBuilder.DropIndex("IX_BondEntries_AccountId_BondDetailsId_PostingDate_EntryId", "BondEntries");
            migrationBuilder.DropIndex("IX_FinancialLabels_BondAccountEntryEntryId", "FinancialLabels");

            AlterIdentityColumn<TOld, TNew>(migrationBuilder, "CurrencyEntries", oldType, newType);
            AlterIdentityColumn<TOld, TNew>(migrationBuilder, "BondEntries", oldType, newType);
            migrationBuilder.AlterColumn<TNew>(
                name: "CurrencyAccountEntryEntryId",
                table: "CurrencyAccountEntryFinancialLabel",
                type: newType,
                nullable: false,
                oldClrType: typeof(TOld),
                oldType: oldType);
            migrationBuilder.AlterColumn<TNew>(
                name: "BondAccountEntryEntryId",
                table: "FinancialLabels",
                type: newType,
                nullable: true,
                oldClrType: typeof(TOld),
                oldType: oldType,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey("PK_CurrencyEntries", "CurrencyEntries", "EntryId");
            migrationBuilder.AddPrimaryKey("PK_BondEntries", "BondEntries", "EntryId");
            migrationBuilder.AddPrimaryKey(
                "PK_CurrencyAccountEntryFinancialLabel",
                "CurrencyAccountEntryFinancialLabel",
                ["CurrencyAccountEntryEntryId", "LabelsId"]);
            migrationBuilder.CreateIndex(
                "IX_CurrencyEntries_AccountId_PostingDate_EntryId",
                "CurrencyEntries",
                ["AccountId", "PostingDate", "EntryId"]);
            migrationBuilder.CreateIndex(
                "IX_BondEntries_AccountId_PostingDate_EntryId",
                "BondEntries",
                ["AccountId", "PostingDate", "EntryId"]);
            migrationBuilder.CreateIndex(
                "IX_BondEntries_AccountId_BondDetailsId_PostingDate_EntryId",
                "BondEntries",
                ["AccountId", "BondDetailsId", "PostingDate", "EntryId"]);
            migrationBuilder.CreateIndex("IX_FinancialLabels_BondAccountEntryEntryId", "FinancialLabels", "BondAccountEntryEntryId");
            migrationBuilder.AddForeignKey(
                _currencyLabelForeignKey,
                "CurrencyAccountEntryFinancialLabel",
                "CurrencyAccountEntryEntryId",
                "CurrencyEntries",
                principalColumn: "EntryId",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                _bondLabelForeignKey,
                "FinancialLabels",
                "BondAccountEntryEntryId",
                "BondEntries",
                principalColumn: "EntryId");
        }

        private static void AlterIdentityColumn<TOld, TNew>(MigrationBuilder migrationBuilder, string table, string oldType, string newType) =>
            migrationBuilder.AlterColumn<TNew>(
                name: "EntryId",
                table: table,
                type: newType,
                nullable: false,
                oldClrType: typeof(TOld),
                oldType: oldType)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
    }
}