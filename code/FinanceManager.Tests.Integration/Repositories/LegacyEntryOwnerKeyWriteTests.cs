using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Bond.Repositories;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public class LegacyEntryOwnerKeyWriteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountWritesStampTrustedKeysAndValidateTheWholeBatch(bool recalculate)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync(ct);
        context.Accounts.AddRange(
            new FinancialAccountBaseDto { AccountId = 1, AccountType = AccountType.Currency, UserId = -5, CurrencyId = 0 },
            new FinancialAccountBaseDto { AccountId = 2, AccountType = AccountType.Currency, UserId = 6, CurrencyId = 7 },
            new FinancialAccountBaseDto { AccountId = 3, AccountType = AccountType.Bond, UserId = -5 },
            new FinancialAccountBaseDto { AccountId = 4, AccountType = AccountType.Bond, UserId = 6 },
            new FinancialAccountBaseDto { AccountId = 5, AccountType = AccountType.Currency, UserId = 0 },
            new FinancialAccountBaseDto { AccountId = 6, AccountType = AccountType.Bond, UserId = 0 });
        await context.SaveChangesAsync(ct);

        var cash = new CurrencyEntryRepository(context);
        var bonds = new BondEntryRepository(context);
        var date = DateTime.UnixEpoch;
        CurrencyAccountEntry Cash(int? id) => new(id, 0, date, 0, 10) { UserId = 99, CurrencyId = 99 };
        BondAccountEntry Bond(int? id) => new(id, 0, date, 0, 3, 10) { UserId = 99 };
        var firstCash = Cash(1);
        var firstBond = Bond(3);
        Assert.True(await cash.Add(firstCash, recalculate, ct));
        Assert.True(await bonds.Add(firstBond, recalculate, ct));
        Assert.True(await cash.Add([Cash(1), Cash(2)], recalculate, ct));
        Assert.True(await bonds.Add([Bond(3), Bond(4)], recalculate, ct));

        foreach (var invalid in new int?[] { null, 99, 3, 5 })
            Assert.False(await cash.Add([Cash(1), Cash(invalid)], recalculate, ct));
        foreach (var invalid in new int?[] { null, 99, 1, 6 })
            Assert.False(await bonds.Add([Bond(3), Bond(invalid)], recalculate, ct));

        var cashRows = await context.CurrencyEntries.AsNoTracking().OrderBy(e => e.EntryId).ToListAsync(ct);
        var bondRows = await context.BondEntries.AsNoTracking().OrderBy(e => e.EntryId).ToListAsync(ct);
        Assert.Equal(new int?[] { -5, -5, 6 }, cashRows.Select(e => e.UserId));
        Assert.Equal(new int?[] { 0, 0, 7 }, cashRows.Select(e => e.CurrencyId));
        Assert.Equal(new int?[] { -5, -5, 6 }, bondRows.Select(e => e.UserId));
        if (recalculate)
        {
            Assert.Equal(new decimal[] { 10, 20, 10 }, cashRows.Select(e => e.Value));
            Assert.Equal(new decimal[] { 3, 6, 3 }, bondRows.Select(e => e.Value));
        }

        // Simulate historical rows missed by expand and a caller supplying conflicting metadata.
        await context.CurrencyEntries.ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, (int?)null).SetProperty(e => e.CurrencyId, (int?)null), ct);
        await context.BondEntries.ExecuteUpdateAsync(s => s.SetProperty(e => e.UserId, (int?)null), ct);
        Assert.True(await cash.Update(firstCash));
        Assert.True(await bonds.Update(firstBond));
        var updated = await context.CurrencyEntries.AsNoTracking().SingleAsync(e => e.EntryId == firstCash.EntryId, ct);
        Assert.Equal(-5, updated.UserId);
        Assert.Equal(0, updated.CurrencyId);
        Assert.Equal(-5, (await context.BondEntries.AsNoTracking().SingleAsync(e => e.EntryId == firstBond.EntryId, ct)).UserId);
    }
}