using FinanceManager.Domain.Administration.Monitoring;
using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.GiftCodes;
using FinanceManager.Domain.Insights.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Infrastructure.Features.Identity.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>
/// <see cref="UserRepository.RemoveUser"/> on both execution paths: relational (ExecuteDelete inside one transaction, with foreign
/// keys enforced by SQLite) and tracked (the in-memory provider). Every row of the deleted user goes, whether or not it is linked
/// to a legacy account; other users, the shared labels and the audit tables stay.
/// Known limit: owned rows have no foreign key to the user, so an import that commits after the delete leaves orphans.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UserDeletionTests : IDisposable
{
    private const int _target = 42;
    private const int _other = 7;
    private static readonly DateTime _jan1 = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Tables that carry a user key but intentionally survive RemoveUser, with the reason.
    private static readonly Dictionary<string, string> _keptTables = new()
    {
        ["Accounts"] = "dormant until the schema contract in #1144",
        ["ActiveUsers"] = "login history is audit data",
        ["GiftCodes"] = "audit actor ids intentionally survive deletion of the account (see GiftCodeConfiguration)",
    };

    // Properties that mark an entity as owned by a user.
    private static readonly string[] _ownerProperties = ["UserId", "CreatedByUserId", "OwnerId"];

    private SqliteConnection? _connection;
    private AppDbContext? _context;

    public void Dispose()
    {
        _context?.Dispose();
        _connection?.Dispose();
    }

    public static TheoryData<string> Providers => ["sqlite", "inmemory"];

    private AppDbContext CreateContext(string provider)
    {
        if (provider == "sqlite")
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
            _context.Database.EnsureCreated();
        }
        else
        {
            _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        }

        return _context;
    }

    private async Task SeedUserData(AppDbContext context, int userId, FinancialLabel sharedLabel, FinancialLabel bondLabel, FinancialLabel legacyBondLabel, int accountId, int entryBase)
    {
        var ct = TestContext.Current.CancellationToken;
        context.Users.Add(new UserDto { Id = userId, Login = $"user{userId}", Password = "hash", CreationDate = _jan1 });
        context.Accounts.Add(new FinancialAccountBaseDto { AccountId = accountId, UserId = userId, Name = $"Account {userId}", AccountLabel = AccountLabel.Cash, AccountType = AccountType.Currency });
        await context.SaveChangesAsync(ct);

        context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(accountId, 0, _jan1, 0, 10m) { UserId = userId, CurrencyId = 0, Labels = [sharedLabel] },
            new CurrencyAccountEntry(null, 0, _jan1, 0, 20m) { UserId = userId, CurrencyId = 0, Labels = [sharedLabel] },
            new CurrencyAccountEntry(accountId, 0, _jan1, 0, 30m) { UserId = userId, CurrencyId = 0 });
        context.BondEntries.AddRange(
            new BondAccountEntry(null, 0, _jan1, 0, 1m, 5) { UserId = userId, Labels = [bondLabel] },
            new BondAccountEntry(accountId, 0, _jan1, 0, 2m, 5) { UserId = userId, Labels = [legacyBondLabel] });
        context.Assets.Add(new Asset { Id = entryBase, Name = $"Equity {userId}", Type = AssetType.Stock });
        context.AssetListings.Add(new AssetListing { Id = entryBase, AssetId = entryBase, Ticker = $"T{userId}", ExchangeMic = "XNAS", ExchangeName = "N", TradingCurrency = "USD", PriceMultiplier = 1m, IsActive = true });
        await context.SaveChangesAsync(ct);

        context.InvestmentTransactions.AddRange(
            new InvestmentTransaction { UserId = userId, AccountId = null, AssetListingId = entryBase, Type = InvestmentTransactionType.Buy, Quantity = 1, UnitPrice = 1m, Currency = "USD", TradeDate = DateOnly.FromDateTime(_jan1), PostingDate = _jan1, Labels = [sharedLabel] },
            new InvestmentTransaction { UserId = userId, AccountId = accountId, AssetListingId = entryBase, Type = InvestmentTransactionType.Buy, Quantity = 1, UnitPrice = 1m, Currency = "USD", TradeDate = DateOnly.FromDateTime(_jan1), PostingDate = _jan1 });
        context.DividendPayments.AddRange(
            new DividendPayment { UserId = userId, AccountId = accountId, AssetListingId = entryBase, PaymentDate = DateOnly.FromDateTime(_jan1), Amount = 1, Currency = "USD" },
            new DividendPayment { UserId = userId, AccountId = null, AssetListingId = entryBase, PaymentDate = DateOnly.FromDateTime(_jan1), Amount = 2, Currency = "USD" });
        context.FinancialAlerts.Add(new FinancialAlert(userId, "Alert", AlertType.LargeTransaction, AlertComparisonOperator.GreaterThan, 5m));
        context.FinancialInsights.Add(new FinancialInsight { UserId = userId, Title = "t", Message = "m", Tags = "x", CreatedAt = _jan1 });
        context.TransactionRules.Add(new TransactionRuleDefinition { UserId = userId, Name = "Rule", Order = 1 });
        context.RecurringSubscriptions.Add(new RecurringSubscription { UserId = userId, MerchantKey = "netflix", Name = "Netflix", ReferenceAmount = 10m });
        context.RefreshTokens.Add(new RefreshToken { UserId = userId, TokenHash = $"refresh-{userId}", FamilyId = Guid.NewGuid(), CreatedAt = _jan1, ExpiresAt = _jan1, AbsoluteExpiresAt = _jan1 });
        context.PasswordResetTokens.Add(new PasswordResetToken { UserId = userId, TokenHash = $"reset-{userId}", CreatedAt = _jan1, ExpiresAt = _jan1 });
        context.ActiveUsers.Add(new ActiveUser { UserId = userId, LoginTime = _jan1 });
        context.GiftCodes.Add(new GiftCode { CodeHash = $"code-{userId}", CodeSuffix = "ABCD", CreatedByUserId = userId, CreatedAtUtc = _jan1 });
        await context.SaveChangesAsync(ct);
    }

    private async Task<AppDbContext> SeedTwoUsers(string provider)
    {
        var context = CreateContext(provider);
        var shared = new FinancialLabel { Name = "Shared" };
        var targetBondLabel = new FinancialLabel { Name = "Target bond label" };
        var otherBondLabel = new FinancialLabel { Name = "Other bond label" };
        // A bond label belongs to one entry, so the legacy account-linked bond entries carry their own labels.
        var targetLegacyBondLabel = new FinancialLabel { Name = "Target legacy bond label" };
        var otherLegacyBondLabel = new FinancialLabel { Name = "Other legacy bond label" };
        context.FinancialLabels.AddRange(shared, targetBondLabel, otherBondLabel, targetLegacyBondLabel, otherLegacyBondLabel);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedUserData(context, _target, shared, targetBondLabel, targetLegacyBondLabel, accountId: 100, entryBase: 1);
        await SeedUserData(context, _other, shared, otherBondLabel, otherLegacyBondLabel, accountId: 200, entryBase: 2);
        context.ChangeTracker.Clear();
        return context;
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task RemoveUser_DeletesEveryRowOfTheUser_AndLeavesEverythingElse(string provider)
    {
        var context = await SeedTwoUsers(provider);
        var ct = TestContext.Current.CancellationToken;

        Assert.True(await new UserRepository(context).RemoveUser(_target, ct));
        context.ChangeTracker.Clear();

        Assert.False(await context.Users.AnyAsync(u => u.Id == _target, ct));
        Assert.True(await context.Accounts.AnyAsync(a => a.UserId == _target, ct));
        Assert.Equal(0, await context.CurrencyEntries.CountAsync(e => e.UserId == _target || e.AccountId == 100, ct));
        Assert.Equal(0, await context.BondEntries.CountAsync(e => e.UserId == _target || e.AccountId == 100, ct));
        Assert.Equal(0, await context.InvestmentTransactions.CountAsync(e => e.UserId == _target, ct));
        Assert.Equal(0, await context.DividendPayments.CountAsync(e => e.UserId == _target || e.AccountId == 100, ct));
        Assert.False(await context.FinancialAlerts.AnyAsync(e => e.UserId == _target, ct));
        Assert.False(await context.FinancialInsights.AnyAsync(e => e.UserId == _target, ct));
        Assert.False(await context.TransactionRules.AnyAsync(e => e.UserId == _target, ct));
        Assert.False(await context.RecurringSubscriptions.AnyAsync(e => e.UserId == _target, ct));
        Assert.False(await context.RefreshTokens.AnyAsync(e => e.UserId == _target, ct));
        Assert.False(await context.PasswordResetTokens.AnyAsync(e => e.UserId == _target, ct));

        // The other user keeps every row.
        Assert.True(await context.Users.AnyAsync(u => u.Id == _other, ct));
        Assert.Equal(3, await context.CurrencyEntries.CountAsync(e => e.UserId == _other || e.AccountId == 200, ct));
        Assert.Equal(2, await context.BondEntries.CountAsync(e => e.UserId == _other || e.AccountId == 200, ct));
        Assert.Equal(2, await context.InvestmentTransactions.CountAsync(e => e.UserId == _other, ct));
        Assert.Equal(2, await context.DividendPayments.CountAsync(e => e.UserId == _other, ct));
        Assert.True(await context.FinancialAlerts.AnyAsync(e => e.UserId == _other, ct));
        Assert.True(await context.RefreshTokens.AnyAsync(e => e.UserId == _other, ct));

        // Shared labels survive and audit rows stay.
        Assert.Equal(5, await context.FinancialLabels.CountAsync(ct));
        Assert.True(await context.ActiveUsers.AnyAsync(a => a.UserId == _target, ct));
        Assert.True(await context.GiftCodes.AnyAsync(g => g.CreatedByUserId == _target, ct));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task RemoveUser_UnlinksOnlyTheDeletedUsersBondLabels_AndRemovesLabelJoinRows(string provider)
    {
        var context = await SeedTwoUsers(provider);
        var ct = TestContext.Current.CancellationToken;
        var otherBondEntryId = await context.BondEntries.Where(e => e.UserId == _other && e.AccountId == null).Select(e => e.EntryId).SingleAsync(ct);

        await new UserRepository(context).RemoveUser(_target, ct);
        context.ChangeTracker.Clear();

        var labels = await context.FinancialLabels.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
        Assert.Equal(["Shared", "Target bond label", "Other bond label", "Target legacy bond label", "Other legacy bond label"], labels.Select(l => l.Name));
        var bondKeys = await context.FinancialLabels.Select(l => new { l.Name, Key = EF.Property<long?>(l, "BondAccountEntryEntryId") }).ToDictionaryAsync(l => l.Name, l => l.Key, ct);
        Assert.Null(bondKeys["Target bond label"]);
        Assert.Null(bondKeys["Target legacy bond label"]);
        Assert.NotNull(bondKeys["Other legacy bond label"]);
        Assert.Equal(otherBondEntryId, bondKeys["Other bond label"]);

        // Only the other user's entries still point at the shared label through the join tables.
        Assert.Equal(2, await context.CurrencyEntries.CountAsync(e => e.Labels.Any(l => l.Name == "Shared"), ct));
        Assert.Equal(1, await context.InvestmentTransactions.CountAsync(e => e.Labels.Any(l => l.Name == "Shared"), ct));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task RemoveUser_MissingUserOrZero_ReturnsFalseAndChangesNothing(string provider)
    {
        var context = await SeedTwoUsers(provider);
        var repository = new UserRepository(context);
        var before = await context.CurrencyEntries.CountAsync(TestContext.Current.CancellationToken);

        Assert.False(await repository.RemoveUser(999, TestContext.Current.CancellationToken));
        Assert.False(await repository.RemoveUser(0, TestContext.Current.CancellationToken));

        Assert.Equal(before, await context.CurrencyEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveUser_FailurePartWayRollsEverythingBack_OnTheRelationalPath()
    {
        var context = await SeedTwoUsers("sqlite");
        var ct = TestContext.Current.CancellationToken;
        // A row that blocks the final user delete makes the transaction fail after most tables were already emptied.
        await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER block_user_delete BEFORE DELETE ON Users BEGIN SELECT RAISE(ABORT, 'blocked'); END;", ct);

        await Assert.ThrowsAnyAsync<Exception>(() => new UserRepository(context).RemoveUser(_target, ct));
        context.ChangeTracker.Clear();

        Assert.True(await context.Users.AnyAsync(u => u.Id == _target, ct));
        Assert.Equal(3, await context.CurrencyEntries.CountAsync(e => e.UserId == _target || e.AccountId == 100, ct));
        Assert.Equal(2, await context.InvestmentTransactions.CountAsync(e => e.UserId == _target, ct));
    }

    [Fact]
    public void EveryTableWithAUserKey_IsDeletedByRemoveUser_OrExplicitlyKept()
    {
        // Owned rows have no foreign key to Users, so the database cannot warn when a new table is forgotten here.
        // A table added later with a user key fails this test until RemoveUser deletes it or it is listed with a reason.
        var context = CreateContext("sqlite");
        var deletedByRemoveUser = new HashSet<string>
        {
            "BondEntries", "CurrencyEntries", "InvestmentTransactions", "DividendPayments", "FinancialAlerts",
            "FinancialInsights", "TransactionRules", "RecurringSubscriptions", "RefreshTokens", "PasswordResetTokens", "Users",
        };

        var tablesWithAUserKey = context.Model.GetEntityTypes()
            .Where(type => !type.IsOwned() && type.GetProperties().Any(property => _ownerProperties.Contains(property.Name)))
            .Select(type => type.GetTableName()!)
            .ToHashSet();
        tablesWithAUserKey.Add("Users");

        var forgotten = tablesWithAUserKey.Except(deletedByRemoveUser).Except(_keptTables.Keys).ToList();
        Assert.True(forgotten.Count == 0, $"RemoveUser does not delete (and no reason keeps) these tables with a user key: {string.Join(", ", forgotten)}.");
        // The lists must not go stale either: every named table still exists in the model.
        var modelTables = context.Model.GetEntityTypes().Select(type => type.GetTableName()).ToHashSet();
        var stale = deletedByRemoveUser.Concat(_keptTables.Keys).Where(table => !modelTables.Contains(table)).ToList();
        Assert.True(stale.Count == 0, $"Tables no longer in the model: {string.Join(", ", stale)}.");
    }
}