using FinanceManager.Api.Migrations;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public class EnforceTransactionOwnerKeysLiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CutoverReportsBlockingRowsThenBackfillsAndEnforcesOwnerKeys(bool sqlServer)
    {
        var variable = PreContractDatabase.ConnectionVariable(sqlServer);
        var connection = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrEmpty(connection), $"Set {variable} to run the owner-key migration on a disposable database.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = await PreContractDatabase.Create(sqlServer, connection!, ct);
        var migration = new EnforceTransactionOwnerKeys();

        await db.Apply(migration, up: false, ct);
        var account = await db.Insert("Accounts", "AccountId", ct, ("UserId", -5), ("Name", "Cash"), ("AccountType", 0), ("AccountLabel", 0), ("CurrencyId", 0));
        var bondAccount = await db.Insert("Accounts", "AccountId", ct, ("UserId", -5), ("Name", "Bonds"), ("AccountType", 2), ("AccountLabel", 0), ("CurrencyId", 0));
        var user = await db.Insert("Users", "Id", ct, ("Login", "cutover"), ("Password", "unused"), ("CreationDate", DateTime.UtcNow),
            ("FailedLoginAttempts", 0), ("PreferredCurrencyId", 0), ("PricingLevel", 0), ("UserRole", 0));
        await db.Insert("Currencies", null, ct, ("Id", 0), ("ShortName", "PLN"), ("Symbol", "PLN"));
        Task<long> Cash(long? accountId, int? userId, int? currencyId) => db.Insert("CurrencyEntries", "EntryId", ct,
            ("AccountId", accountId), ("UserId", userId), ("CurrencyId", currencyId), ("PostingDate", DateTime.UnixEpoch),
            ("Description", ""), ("Value", 1m), ("ValueChange", 1m));
        var linked = await Cash(account, null, null);
        var mismatch = await Cash(account, 99, 0);
        var invalidCurrency = await Cash(null, -5, 999);
        var zero = await Cash(null, 0, 0);
        var ownerOnly = await Cash(null, -5, 0);
        var orphan = await Cash(int.MaxValue, -10, 0);
        await db.Insert("BondEntries", "EntryId", ct, ("AccountId", bondAccount), ("UserId", null), ("BondDetailsId", 9),
            ("PostingDate", DateTime.UnixEpoch), ("Description", ""), ("Value", 1m), ("ValueChange", 1m));
        var alert = await AddAlert(db, (int)user, account, ct);
        await AddTrade(db, (long)int.MaxValue + 1, ct);

        var failure = await Assert.ThrowsAnyAsync<DbException>(() => db.Apply(migration, up: true, ct));
        Assert.Contains("null_or_zero_keys=1, invalid_cash_currency=1, account_mismatch=1, legacy_alert_scope=1", failure.Message);
        Assert.Equal(1, await db.Scalar("""SELECT COUNT(*) FROM "BondEntries" WHERE "UserId" IS NULL""", ct));

        await db.Execute($"""DELETE FROM "CurrencyEntries" WHERE "EntryId" IN ({mismatch}, {zero}, {invalidCurrency})""", ct);
        await db.Execute($"""DELETE FROM "FinancialAlerts" WHERE "Id" = '{alert}'""", ct);
        await db.Apply(migration, up: true, ct);
        var cash = await db.Context.CurrencyEntries.AsNoTracking().SingleAsync(e => e.EntryId == linked, ct);
        Assert.Equal((-5, 0), (cash.UserId, cash.CurrencyId));
        Assert.Equal(-5, (await db.Context.BondEntries.AsNoTracking().SingleAsync(ct)).UserId);
        Assert.Equal(0, await db.Scalar($"""SELECT COUNT(*) FROM "CurrencyEntries" WHERE "EntryId" = {ownerOnly} AND "AccountId" IS NOT NULL""", ct));
        Assert.Equal(-10, (await db.Context.CurrencyEntries.AsNoTracking().SingleAsync(e => e.EntryId == orphan, ct)).UserId);
        await Assert.ThrowsAnyAsync<DbException>(() => db.Execute("""UPDATE "BondEntries" SET "UserId" = NULL""", ct));
        await db.Apply(migration, up: false, ct);
        Assert.Equal(1, await db.Execute("""UPDATE "BondEntries" SET "UserId" = NULL""", ct));
    }

    internal static async Task<Guid> AddAlert(PreContractDatabase db, int userId, long? accountId, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await db.Insert("FinancialAlerts", null, ct, ("Id", id), ("UserId", userId), ("Title", "Alert"), ("AlertType", 0), ("IsEnabled", true),
            ("ComparisonOperator", 0), ("Threshold", 0m), ("EvaluationPeriod", 0), ("LastStatus", 0), ("CreatedAt", DateTime.UtcNow), ("AccountId", accountId));
        return id;
    }

    internal static async Task<long> AddTrade(PreContractDatabase db, long userId, CancellationToken ct, long? accountId = null, decimal value = 0m)
    {
        var now = DateTimeOffset.UtcNow;
        var asset = await db.Insert("Assets", "Id", ct, ("Name", "Cutover"), ("Type", 0), ("CreatedAt", now), ("UpdatedAt", now));
        var listing = await db.Insert("AssetListings", "Id", ct, ("AssetId", asset), ("Ticker", "TEST"), ("ExchangeName", ""), ("IsActive", true),
            ("IsPrimaryListing", false), ("PriceMultiplier", 1m), ("CreatedAt", now), ("UpdatedAt", now));
        return await db.Insert("InvestmentTransactions", "Id", ct, ("UserId", userId), ("AssetListingId", listing), ("Type", 0), ("Quantity", 1m),
            ("UnitPrice", 1m), ("Currency", "PLN"), ("PostingDate", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)), ("Description", ""),
            ("CreatedAt", now), ("UpdatedAt", now), ("AccountId", accountId), ("Value", value));
    }
}