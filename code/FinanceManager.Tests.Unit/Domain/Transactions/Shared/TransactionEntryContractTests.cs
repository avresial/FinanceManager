using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Domain.Transactions.Shared;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Domain.Transactions.Shared;

[Trait("Category", "Unit")]
public class TransactionEntryContractTests
{
    private static readonly DateTime _postingDate = new(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TransactionModelsOwnTheirStateWithoutImplementationParents()
    {
        Assert.Equal(typeof(object), typeof(CashEntry).BaseType);
        Assert.Equal(typeof(object), typeof(BondEntry).BaseType);
        Assert.Equal(typeof(object), typeof(InvestmentTransaction).BaseType);
        Assert.IsAssignableFrom<ILedgerEntry>(new CashEntry(2, _postingDate, 1m));
        Assert.IsAssignableFrom<ILedgerEntry>(new BondEntry(2, _postingDate, 1m, 3));
    }

    [Fact]
    public void CurrencyEntry_ConstructorSetsIdentity()
    {
        var entry = new CashEntry(5_000_000_000L, _postingDate, 1m);

        Assert.Equal(5_000_000_000L, entry.EntryId);
    }

    [Fact]
    public void EntryRepositories_CanSetConcreteEntryIdsThroughReflection()
    {
        // Mirrors CashEntryRepository / BondEntryRepository: the setter is internal, so it is only reachable by reflection.
        var currency = new CashEntry(0, _postingDate, 1m);
        var bond = new BondEntry(0, _postingDate, 1m, 3);
        typeof(CashEntry).GetProperty(nameof(CashEntry.EntryId))!.SetValue(currency, 5_000_000_000L);
        typeof(BondEntry).GetProperty(nameof(BondEntry.EntryId))!.SetValue(bond, 42L);

        Assert.Equal(5_000_000_000L, currency.EntryId);
        Assert.Equal(42L, bond.EntryId);
    }

    [Fact]
    public void CurrencyEntry_SerializesWithUnchangedPropertyOrder()
    {
        var entry = new CashEntry(2, _postingDate, 1m) { Description = "d", ContractorDetails = "c" };

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"description":"d","contractorDetails":"c","entryId":2,"postingDate":"2026-05-17T00:00:00Z","valueChange":1,"labels":[]}""",
            json);
    }

    [Fact]
    public void BondEntry_SerializesDescriptionAfterBondDetailsId()
    {
        var entry = new BondEntry(2, _postingDate, 1m, 3) { Description = "Quarterly top-up" };

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"bondDetailsId":3,"description":"Quarterly top-up","entryId":2,"postingDate":"2026-05-17T00:00:00Z","valueChange":1,"labels":[]}""",
            json);
    }

    [Fact]
    public void Description_DefaultsToEmptyOnEveryEntryType()
    {
        Assert.Equal(string.Empty, new CashEntry(2, _postingDate, 1m).Description);
        Assert.Equal(string.Empty, new BondEntry(2, _postingDate, 1m, 3).Description);
        Assert.Equal(string.Empty, new InvestmentTransaction().Description);
    }

    [Fact]
    public void BondEntry_SerializesWithUnchangedPropertyOrder()
    {
        var entry = new BondEntry(2, _postingDate, 1m, 3);

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            """{"bondDetailsId":3,"description":"","entryId":2,"postingDate":"2026-05-17T00:00:00Z","valueChange":1,"labels":[]}""",
            json);
    }

    [Theory]
    [InlineData(InvestmentTransactionType.Buy, 2.5, 2.5)]
    [InlineData(InvestmentTransactionType.Sell, 2.5, -2.5)]
    public void InvestmentValueChange_ReturnsSignedQuantity(InvestmentTransactionType type, decimal quantity, decimal expected)
    {
        InvestmentTransaction entry = new InvestmentTransaction { Type = type, Quantity = quantity };

        Assert.Equal(expected, entry.ValueChange);
    }

    [Theory]
    [InlineData(InvestmentTransactionType.Buy)]
    [InlineData(InvestmentTransactionType.Sell)]
    public void InvestmentValueChange_SetterRoundTripsAndPreservesDirectionAtZero(InvestmentTransactionType initialType)
    {
        var transaction = new InvestmentTransaction { Type = initialType, Quantity = 3m };
        InvestmentTransaction entry = transaction;

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
            """{"description":"","userId":2,"assetListingId":3,"assetListing":null,"type":1,"quantity":2.5,"unitPrice":10,"currency":"USD","fee":null,"createdAt":"0001-01-01T00:00:00+00:00","updatedAt":"0001-01-01T00:00:00+00:00","signedQuantity":-2.5,"entryId":0,"postingDate":"2026-05-17T00:00:00Z","labels":[]}""",
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

        Assert.Null(context.Model.FindEntityType(typeof(InvestmentTransaction))!.FindProperty(nameof(CashEntry.ValueChange)));
        foreach (var type in new[] { typeof(CashEntry), typeof(BondEntry) })
        {
            var property = context.Model.FindEntityType(type)!.FindProperty(nameof(CashEntry.ValueChange));
            Assert.NotNull(property);
            Assert.Equal("ValueChange", property.GetColumnName());
            Assert.Equal(typeof(decimal), property.ClrType);
        }
    }

    [Fact]
    public void EfModel_UsesStandaloneEntryModelsAndKeepsIdColumn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=model-only");
        using var context = new AppDbContext(options.Options);

        Assert.Null(context.Model.FindEntityType(typeof(InvestmentTransaction))?.BaseType);
        Assert.Null(context.Model.FindEntityType(typeof(CashEntry))?.BaseType);

        var investment = context.Model.FindEntityType(typeof(InvestmentTransaction))!;
        var entryId = investment.FindProperty(nameof(InvestmentTransaction.EntryId))!;
        Assert.Equal("Id", entryId.GetColumnName());
        Assert.Equal(typeof(long), entryId.ClrType);
    }

    [Fact]
    public void InvestmentTransaction_JsonRoundTrip_PreservesEntryId()
    {
        const long entryId = 5_000_000_000;
        var transaction = new InvestmentTransaction { EntryId = entryId };

        var roundTripped = JsonSerializer.Deserialize<InvestmentTransaction>(JsonSerializer.Serialize(transaction));

        Assert.Equal(entryId, roundTripped!.EntryId);
    }

    [Fact]
    public void Update_ChangesTheDeltaAndDetails()
    {
        var cash = new CashEntry(2, _postingDate, 10m);
        var bond = new BondEntry(2, _postingDate, 10m, 3);

        cash.Update(new CashEntry(2, _postingDate, 25m) { Description = "changed" });
        bond.Update(new BondEntry(2, _postingDate, 25m, 4));

        Assert.Equal(25m, cash.ValueChange);
        Assert.Equal("changed", cash.Description);
        Assert.Equal(25m, bond.ValueChange);
        Assert.Equal(4, bond.BondDetailsId);
    }
}