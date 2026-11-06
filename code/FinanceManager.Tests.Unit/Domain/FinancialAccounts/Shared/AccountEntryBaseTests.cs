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
    public void BondEntry_SerializesDescriptionAfterBondDetailsId()
    {
        var entry = new BondAccountEntry(1, 2, _postingDate, 10m, 1m, 3) { Description = "Quarterly top-up" };

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"bondDetailsId":3,"description":"Quarterly top-up","entryId":2,"accountId":1,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":1,"labels":[]}""",
            json);
    }

    [Fact]
    public void Description_DefaultsToEmptyOnEveryEntryType()
    {
        AccountEntryBase[] entries = [new CurrencyAccountEntry(1, 2, _postingDate, 10m, 1m), new BondAccountEntry(1, 2, _postingDate, 10m, 1m, 3), new InvestmentTransaction()];

        Assert.All(entries, entry => Assert.Equal(string.Empty, entry.Description));
    }

    [Fact]
    public void BondEntry_SerializesWithUnchangedPropertyOrder()
    {
        var entry = new BondAccountEntry(1, 2, _postingDate, 10m, 1m, 3);

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"bondDetailsId":3,"description":"","entryId":2,"accountId":1,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":1,"labels":[]}""",
            json);
    }

    [Theory]
    [InlineData(InvestmentTransactionType.Buy, 2.5, 2.5)]
    [InlineData(InvestmentTransactionType.Sell, 2.5, -2.5)]
    public void InvestmentValueChange_ReturnsSignedQuantity(InvestmentTransactionType type, decimal quantity, decimal expected)
    {
        AccountEntryBase entry = new InvestmentTransaction { Type = type, Quantity = quantity };

        Assert.Equal(expected, entry.ValueChange);
    }

    [Theory]
    [InlineData(InvestmentTransactionType.Buy)]
    [InlineData(InvestmentTransactionType.Sell)]
    public void InvestmentValueChange_SetterRoundTripsAndPreservesDirectionAtZero(InvestmentTransactionType initialType)
    {
        var transaction = new InvestmentTransaction { Type = initialType, Quantity = 3m };
        AccountEntryBase entry = transaction;

        foreach (var value in new[] { 0m, -2.5m, 0m, 4.25m, 0m, -1m })
        {
            var previousType = transaction.Type;
            entry.ValueChange = value;

            Assert.Equal(value, entry.ValueChange);
            Assert.Equal(Math.Abs(value), transaction.Quantity);
            Assert.Equal(value == 0 ? previousType : value > 0 ? InvestmentTransactionType.Buy : InvestmentTransactionType.Sell, transaction.Type);
        }
    }

    [Fact]
    public void InvestmentEntry_SerializesPostingDateAndIgnoresComputedTradeDate()
    {
        var entry = new InvestmentTransaction
        {
            AccountId = 1,
            UserId = 2,
            AssetListingId = 3,
            Type = InvestmentTransactionType.Sell,
            Quantity = 2.5m,
            UnitPrice = 10m,
            Currency = "USD",
            TradeDate = new DateOnly(2026, 5, 17)
        };

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"description":"","userId":2,"assetListingId":3,"assetListing":null,"type":1,"quantity":2.5,"unitPrice":10,"currency":"USD","fee":null,"createdAt":"0001-01-01T00:00:00+00:00","updatedAt":"0001-01-01T00:00:00+00:00","signedQuantity":-2.5,"entryId":0,"accountId":1,"postingDate":"2026-05-17T00:00:00Z"}""",
            json);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void InvestmentPostingDate_NormalizesCalendarDateToUtcMidnight(DateTimeKind kind)
    {
        var entry = new InvestmentTransaction { PostingDate = new DateTime(2026, 5, 17, 23, 42, 0, kind) };
        Assert.Equal(new DateOnly(2026, 5, 17), entry.TradeDate);
        Assert.Equal(DateTimeKind.Utc, entry.PostingDate.Kind);
        Assert.Equal(TimeSpan.Zero, entry.PostingDate.TimeOfDay);
        entry.TradeDate = new DateOnly(2024, 2, 29);
        Assert.Equal(new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc), entry.PostingDate);
    }

    [Fact]
    public void EfModel_ValueChangeIsMappedOnlyForFinancialEntries()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=model-only");
        using var context = new AppDbContext(options.Options);

        Assert.Null(context.Model.FindEntityType(typeof(InvestmentTransaction))!.FindProperty(nameof(AccountEntryBase.ValueChange)));
        foreach (var type in new[] { typeof(CurrencyAccountEntry), typeof(BondAccountEntry) })
        {
            var property = context.Model.FindEntityType(type)!.FindProperty(nameof(AccountEntryBase.ValueChange));
            Assert.NotNull(property);
            Assert.Equal("ValueChange", property.GetColumnName());
            Assert.Equal(typeof(decimal), property.ClrType);
        }
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