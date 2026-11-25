using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceManager.Api.Migrations;

/// <summary>
/// Schema contract of epic #1126: drops the account links, the stored running values and the Accounts table.
/// Owner keys, owner indexes and the label join tables stay. No row is deleted or rewritten; the account names in
/// Accounts are the only data lost, so take the pre-contract backup first (docs/architecture/operations/migrations.md).
/// </summary>
public partial class DropFinancialAccounts : Migration
{
    private static readonly (string Table, string Index)[] _accountIndexes =
    [
        ("CurrencyEntries", "IX_CurrencyEntries_AccountId_PostingDate_EntryId"),
        ("BondEntries", "IX_BondEntries_AccountId_PostingDate_EntryId"),
        ("BondEntries", "IX_BondEntries_AccountId_BondDetailsId_PostingDate_EntryId"),
        ("InvestmentTransactions", "IX_InvestmentTransactions_AccountId"),
        ("InvestmentTransactions", "IX_InvestmentTransactions_AccountId_PostingDate_Id"),
        ("DividendPayments", "IX_DividendPayments_AccountId"),
        ("DividendPayments", "IX_DividendPayments_UserId_AccountId_PaymentDate_Id"),
        ("Accounts", "IX_Accounts_AccountType_UserId"),
    ];

    private static readonly (string Table, string Column)[] _droppedColumns =
    [
        ("CurrencyEntries", "AccountId"),
        ("BondEntries", "AccountId"),
        ("InvestmentTransactions", "AccountId"),
        ("DividendPayments", "AccountId"),
        ("FinancialAlerts", "AccountId"),
        ("FinancialInsights", "AccountId"),
        ("CurrencyEntries", "Value"),
        ("BondEntries", "Value"),
        ("InvestmentTransactions", "Value"),
    ];

    // SQL Server refuses to drop a column that an index or foreign key still uses, so the order is FK, indexes, columns, table.
    // EF's SQL Server DropColumn also drops a column's default constraint (InvestmentTransactions.Value has one from
    // StoreInvestmentRunningUnits). PostgreSQL drops the Accounts identity sequence with the table.
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_DividendPayments_Accounts_AccountId", table: "DividendPayments");

        foreach (var (table, index) in _accountIndexes)
            migrationBuilder.DropIndex(name: index, table: table);

        foreach (var (table, column) in _droppedColumns)
            migrationBuilder.DropColumn(name: column, table: table);

        migrationBuilder.DropTable(name: "Accounts");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Restore from the pre-contract backup.");
}