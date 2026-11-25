using FinanceManager.Api.Migrations;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
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
        var variable = sqlServer ? "FM_VALUE_SQLSERVER_CONNECTION" : "FM_VALUE_POSTGRESQL_CONNECTION";
        var connection = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrEmpty(connection), $"Set {variable} to run the owner-key migration on a disposable database.");
        var database = $"fm_cutover_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
            options.UseSqlServer(new SqlConnectionStringBuilder(connection) { InitialCatalog = database }.ConnectionString);
        else
            options.UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { Database = database }.ConnectionString);

        var ct = TestContext.Current.CancellationToken;
        await using var context = new AppDbContext(options.Options);
        try
        {
            await context.Database.EnsureCreatedAsync(ct);
            var migration = new EnforceTransactionOwnerKeys { ActiveProvider = context.Database.ProviderName! };
            async Task Apply(bool up)
            {
                await using var transaction = await context.Database.BeginTransactionAsync(ct);
                var commands = context.GetService<IMigrationsSqlGenerator>().Generate(up ? migration.UpOperations : migration.DownOperations);
                foreach (var command in commands)
                    await context.Database.ExecuteSqlRawAsync(command.CommandText, ct);
                await transaction.CommitAsync(ct);
            }

            await Apply(up: false);
            var account = new FinancialAccountBaseDto { UserId = -5, CurrencyId = 0, AccountType = AccountType.Currency };
            var bondAccount = new FinancialAccountBaseDto { UserId = -5, AccountType = AccountType.Bond };
            var user = new UserDto { Login = "cutover", Password = "unused", CreationDate = DateTime.UtcNow };
            context.AddRange(account, bondAccount, user, new Currency(0, "PLN", "PLN"));
            await context.SaveChangesAsync(ct);
            var linked = new CashEntry(account.AccountId, 0, DateTime.UnixEpoch, 1, 1);
            var mismatch = new CashEntry(account.AccountId, 0, DateTime.UnixEpoch, 1, 1) { UserId = 99 };
            var invalidCurrency = new CashEntry(null, 0, DateTime.UnixEpoch, 1, 1) { UserId = -5, CurrencyId = 999 };
            var zero = new CashEntry(null, 0, DateTime.UnixEpoch, 1, 1);
            var ownerOnly = new CashEntry(null, 0, DateTime.UnixEpoch, 1, 1) { UserId = -5 };
            var orphan = new CashEntry(int.MaxValue, 0, DateTime.UnixEpoch, 1, 1) { UserId = -10 };
            var bond = new BondEntry(bondAccount.AccountId, 0, DateTime.UnixEpoch, 1, 1, 9);
            var alert = new FinancialAlert { UserId = user.Id, AccountId = account.AccountId };
            var trade = new InvestmentTransaction
            {
                UserId = (long)int.MaxValue + 1,
                Currency = "PLN",
                TradeDate = new(2026, 1, 1),
                AssetListing = new AssetListing { Ticker = "TEST", Asset = new Asset { Name = "Cutover" } },
            };
            context.AddRange(linked, mismatch, invalidCurrency, zero, ownerOnly, orphan, bond, alert, trade);
            await context.SaveChangesAsync(ct);
            await context.Database.ExecuteSqlAsync($"""UPDATE "CurrencyEntries" SET "UserId" = NULL, "CurrencyId" = NULL WHERE "EntryId" = {linked.EntryId}""", ct);
            await context.Database.ExecuteSqlRawAsync("""UPDATE "BondEntries" SET "UserId" = NULL""", ct);

            var failure = await Assert.ThrowsAnyAsync<DbException>(() => Apply(up: true));
            Assert.Contains("null_or_zero_keys=1, invalid_cash_currency=1, account_mismatch=1, legacy_alert_scope=1", failure.Message);
            Assert.Equal(1, await context.Database.SqlQueryRaw<int>("""SELECT COUNT(*) AS "Value" FROM "BondEntries" WHERE "UserId" IS NULL""").SingleAsync(ct));

            context.RemoveRange(mismatch, zero, invalidCurrency, alert);
            await context.SaveChangesAsync(ct);
            await Apply(up: true);
            context.ChangeTracker.Clear();
            var cash = await context.CurrencyEntries.SingleAsync(e => e.EntryId == linked.EntryId, ct);
            Assert.Equal((-5, 0), (cash.UserId, cash.CurrencyId));
            Assert.Equal(-5, (await context.BondEntries.SingleAsync(ct)).UserId);
            Assert.Null((await context.CurrencyEntries.SingleAsync(e => e.EntryId == ownerOnly.EntryId, ct)).AccountId);
            Assert.Equal(-10, (await context.CurrencyEntries.SingleAsync(e => e.EntryId == orphan.EntryId, ct)).UserId);
            await Assert.ThrowsAnyAsync<DbException>(() => context.Database.ExecuteSqlRawAsync("""UPDATE "BondEntries" SET "UserId" = NULL""", ct));
            await Apply(up: false);
            Assert.Equal(1, await context.Database.ExecuteSqlRawAsync("""UPDATE "BondEntries" SET "UserId" = NULL""", ct));
        }
        finally
        {
            await context.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }
}