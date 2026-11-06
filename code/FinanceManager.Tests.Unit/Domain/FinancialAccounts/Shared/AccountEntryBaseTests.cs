using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Domain.FinancialAccounts.Shared;

[Trait("Category", "Unit")]
public class AccountEntryBaseTests
{
    private static readonly DateTime _postingDate = new(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AccountEntryBase_IsAbstractAndParentOfAllEntryTypes()
    {
        Assert.True(typeof(AccountEntryBase).IsAbstract);
        Assert.Equal(typeof(AccountEntryBase), typeof(FinancialEntryBase).BaseType);
        Assert.Equal(typeof(AccountEntryBase), typeof(InvestmentTransaction).BaseType);
        Assert.IsAssignableFrom<AccountEntryBase>(new CurrencyAccountEntry(1, 2, _postingDate, 10m, 1m));
        Assert.IsAssignableFrom<AccountEntryBase>(new BondAccountEntry(1, 2, _postingDate, 10m, 1m, 3));
    }

    [Fact]
    public void FinancialEntryBase_ConstructorSetsInheritedIdentity()
    {
        var entry = new FinancialEntryBase(7, 5_000_000_000L, _postingDate, 10m, 1m);

        Assert.Equal(7, entry.AccountId);
        Assert.Equal(5_000_000_000L, entry.EntryId);
    }

    [Fact]
    public void InvestmentTransaction_InitializerSetsAccountId()
    {
        var transaction = new InvestmentTransaction { AccountId = 9 };

        Assert.Equal(9, transaction.AccountId);
        Assert.Equal(0, transaction.EntryId);
    }

    [Fact]
    public void EntryRepositories_CanSetEntryIdThroughFinancialEntryBaseReflection()
    {
        // Mirrors CurrencyEntryRepository / BondEntryRepository: the setter is internal, so it is only reachable by reflection.
        var property = typeof(FinancialEntryBase).GetProperty(nameof(FinancialEntryBase.EntryId));
        Assert.NotNull(property);
        var currency = new CurrencyAccountEntry(1, 0, _postingDate, 10m, 1m);
        var bond = new BondAccountEntry(1, 0, _postingDate, 10m, 1m, 3);

        property.SetValue(currency, 5_000_000_000L);
        property.SetValue(bond, 42L);

        Assert.Equal(5_000_000_000L, currency.EntryId);
        Assert.Equal(42L, bond.EntryId);
    }

    [Fact]
    public void CurrencyEntry_SerializesWithUnchangedPropertyOrder()
    {
        var entry = new CurrencyAccountEntry(1, 2, _postingDate, 10m, 1m) { Description = "d", ContractorDetails = "c" };

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"description":"d","contractorDetails":"c","entryId":2,"accountId":1,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":1,"labels":[]}""",
            json);
    }

    [Fact]
    public void BondEntry_SerializesWithUnchangedPropertyOrder()
    {
        var entry = new BondAccountEntry(1, 2, _postingDate, 10m, 1m, 3);

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"bondDetailsId":3,"entryId":2,"accountId":1,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":1,"labels":[]}""",
            json);
    }

    [Fact]
    public void EfModel_DoesNotMapAccountEntryBaseAndKeepsIdColumn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=model-only");
        using var context = new AppDbContext(options.Options);

        Assert.Null(context.Model.FindEntityType(typeof(AccountEntryBase)));
        Assert.Null(context.Model.FindEntityType(typeof(FinancialEntryBase)));
        Assert.Null(context.Model.FindEntityType(typeof(InvestmentTransaction))?.BaseType);
        Assert.Null(context.Model.FindEntityType(typeof(CurrencyAccountEntry))?.BaseType);

        var investment = context.Model.FindEntityType(typeof(InvestmentTransaction))!;
        var entryId = investment.FindProperty(nameof(InvestmentTransaction.EntryId))!;
        Assert.Equal("Id", entryId.GetColumnName());
        Assert.Equal(typeof(long), entryId.ClrType);
    }

    [Fact]
    public void InvestmentTransaction_JsonRoundTrip_PreservesEntryId()
    {
        const long entryId = 5_000_000_000;
        var transaction = new InvestmentTransaction { EntryId = entryId, AccountId = 3 };

        var roundTripped = JsonSerializer.Deserialize<InvestmentTransaction>(JsonSerializer.Serialize(transaction));

        Assert.Equal(entryId, roundTripped!.EntryId);
        Assert.Equal(3, roundTripped.AccountId);
    }
}