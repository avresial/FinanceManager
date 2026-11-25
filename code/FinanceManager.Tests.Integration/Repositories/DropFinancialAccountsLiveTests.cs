using FinanceManager.Api.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public class DropFinancialAccountsLiveTests
{
    private static readonly string[] _ledgerTables =
        ["CurrencyEntries", "BondEntries", "InvestmentTransactions", "DividendPayments", "FinancialAlerts", "FinancialInsights",
         "FinancialLabels", "CurrencyAccountEntryFinancialLabel", "InvestmentTransactionFinancialLabel"];

    private static readonly string[] _ownerIndexes =
        ["IX_CurrencyEntries_OwnerCurrencyDateId", "IX_CurrencyEntries_OwnerDateId", "IX_BondEntries_OwnerBondDateId", "IX_BondEntries_OwnerDateId",
         "IX_InvestmentTransactions_OwnerListingDateId", "IX_InvestmentTransactions_UserId_AssetListingId", "IX_DividendPayments_OwnerPaymentDateId"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Contract_DropsTheAccountSchema_KeepsEveryRowAndLabelLink_AndRefusesDown(bool sqlServer)
    {
        var variable = PreContractDatabase.ConnectionVariable(sqlServer);
        var connection = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrEmpty(connection), $"Set {variable} to run the contract migration on a disposable database.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = await PreContractDatabase.Create(sqlServer, connection!, ct);

        // Pre-contract rows: some still carry an account link and a running value, others are owner-only.
        var owner = (int)await db.Insert("Users", "Id", ct, ("Login", "contract"), ("Password", "unused"), ("CreationDate", DateTime.UtcNow),
            ("FailedLoginAttempts", 0), ("PreferredCurrencyId", 0), ("PricingLevel", 0), ("UserRole", 0));
        var account = await db.Insert("Accounts", "AccountId", ct, ("UserId", owner), ("Name", "Wallet"), ("AccountType", 0), ("AccountLabel", 0), ("CurrencyId", 0));
        var label = await db.Insert("FinancialLabels", "Id", ct, ("Name", "Food"));
        var bondLabel = await db.Insert("FinancialLabels", "Id", ct, ("Name", "Savings"));
        foreach (var (accountId, value) in new (long?, decimal)[] { (account, 10m), (null, 0m) })
        {
            var cash = await db.Insert("CurrencyEntries", "EntryId", ct, ("AccountId", accountId), ("UserId", owner), ("CurrencyId", 0),
                ("PostingDate", DateTime.UnixEpoch), ("Description", "cash"), ("Value", value), ("ValueChange", 10m));
            await db.Insert("CurrencyAccountEntryFinancialLabel", null, ct, ("CurrencyAccountEntryEntryId", cash), ("LabelsId", label));
        }
        var bond = await db.Insert("BondEntries", "EntryId", ct, ("AccountId", account), ("UserId", owner), ("BondDetailsId", 9),
            ("PostingDate", DateTime.UnixEpoch), ("Description", "bond"), ("Value", 3m), ("ValueChange", 3m));
        await db.Execute($"""UPDATE "FinancialLabels" SET "BondAccountEntryEntryId" = {bond} WHERE "Id" = {bondLabel}""", ct);
        var trade = await EnforceTransactionOwnerKeysLiveTests.AddTrade(db, owner, ct, account, value: 1m);
        await db.Insert("InvestmentTransactionFinancialLabel", null, ct, ("InvestmentTransactionEntryId", trade), ("LabelsId", label));
        var listing = await db.Scalar($"""SELECT "AssetListingId" FROM "InvestmentTransactions" WHERE "Id" = {trade}""", ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var accountId in new long?[] { account, null })
            await db.Insert("DividendPayments", "Id", ct, ("UserId", owner), ("AccountId", accountId), ("AssetListingId", listing),
                ("PaymentDate", new DateOnly(2026, 1, 1)), ("Amount", 1m), ("Currency", "PLN"), ("CreatedAt", now), ("UpdatedAt", now));
        await EnforceTransactionOwnerKeysLiveTests.AddAlert(db, owner, null, ct);
        await db.Insert("FinancialInsights", "Id", ct, ("UserId", owner), ("AccountId", account), ("Title", "t"), ("Message", "m"), ("Tags", "x"), ("CreatedAt", DateTime.UtcNow));
        var before = await RowCounts(db, ct);

        if (sqlServer) await db.Apply(new DropFinancialAccounts(), up: true, ct);
        else await db.Context.GetService<IMigrator>().MigrateAsync("DropFinancialAccounts", ct);

        Assert.Equal(before, await RowCounts(db, ct));
        Assert.Equal(0, await db.Scalar("""SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounts'""", ct));
        Assert.Equal(0, await db.Scalar("""SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE COLUMN_NAME = 'AccountId' OR (COLUMN_NAME = 'Value' AND TABLE_NAME IN ('CurrencyEntries', 'BondEntries', 'InvestmentTransactions'))""", ct));
        var indexes = string.Join(", ", _ownerIndexes.Select(index => $"'{index}'"));
        Assert.Equal(_ownerIndexes.Length, await db.Scalar(sqlServer
            ? $"SELECT COUNT(*) FROM sys.indexes WHERE name IN ({indexes})"
            : $"SELECT COUNT(*) FROM pg_indexes WHERE indexname IN ({indexes})", ct));

        // The current model reads the contracted schema, labels included.
        db.Context.ChangeTracker.Clear();
        Assert.All(await db.Context.CurrencyEntries.Include(e => e.Labels).ToListAsync(ct), e => Assert.Equal("Food", Assert.Single(e.Labels).Name));
        Assert.Equal("Savings", Assert.Single((await db.Context.BondEntries.Include(e => e.Labels).SingleAsync(ct)).Labels).Name);
        Assert.Equal("Food", Assert.Single((await db.Context.InvestmentTransactions.Include(e => e.Labels).SingleAsync(ct)).Labels).Name);
        Assert.Equal(2, await db.Context.DividendPayments.CountAsync(ct));
        Assert.Equal(1, await db.Context.FinancialInsights.CountAsync(ct));
        Assert.Equal(1, await db.Context.FinancialAlerts.CountAsync(ct));

        var refusal = Assert.Throws<NotSupportedException>(() => new DropFinancialAccounts { ActiveProvider = db.Context.Database.ProviderName! }.DownOperations);
        Assert.Equal("Restore from the pre-contract backup.", refusal.Message);
    }

    private static async Task<List<long>> RowCounts(PreContractDatabase db, CancellationToken ct)
    {
        List<long> counts = [];
        foreach (var table in _ledgerTables)
            counts.Add(await db.Scalar($"""SELECT COUNT(*) FROM "{table}" """, ct));
        return counts;
    }
}