using FinanceManager.Contracts.FinancialAccounts.Bond;
using FinanceManager.Contracts.FinancialAccounts.Currencies;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Domain.FinancialAccounts.Shared;

[Trait("Category", "Unit")]
public class AccountModelCompatibilityTests
{
    [Fact]
    public void AccountsEntriesAndResponseDtosHaveNoImplementationParents()
    {
        Type[] models = [typeof(CurrencyAccount), typeof(BondAccount), typeof(InvestmentAccount),
            typeof(CurrencyAccountEntry), typeof(BondAccountEntry), typeof(InvestmentTransaction),
            typeof(CurrencyAccountDto), typeof(BondAccountDto), typeof(CurrencyAccountEntryDto), typeof(BondAccountEntryDto)];
        Assert.All(models, model => Assert.Equal(typeof(object), model.BaseType));
        Assert.DoesNotContain(typeof(IAccount).GetProperties(), property => property.Name is "Start" or "End");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RelationalModelsRetainConcreteTablesAndFieldAccess(bool sqlServer)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer) builder.UseSqlServer("Server=localhost;Database=model-only;TrustServerCertificate=true");
        else builder.UseNpgsql("Host=localhost;Database=model-only");
        using var context = new AppDbContext(builder.Options);

        var cash = context.Model.FindEntityType(typeof(CurrencyAccountEntry))!;
        var bond = context.Model.FindEntityType(typeof(BondAccountEntry))!;
        var trade = context.Model.FindEntityType(typeof(InvestmentTransaction))!;
        var dividend = context.Model.FindEntityType(typeof(DividendPayment))!;
        Assert.All(new[] { cash, bond, trade, dividend }, entity => Assert.True(entity.FindProperty("AccountId")!.IsNullable));
        Assert.False(cash.FindProperty("CurrencyId")!.IsNullable);
        Assert.False(cash.FindProperty("UserId")!.IsNullable);
        Assert.False(bond.FindProperty("UserId")!.IsNullable);
        Assert.Equal(typeof(long), trade.FindProperty("UserId")!.ClrType);
        Assert.Equal(typeof(int), dividend.FindProperty("UserId")!.ClrType);
        Assert.Equal(DeleteBehavior.ClientNoAction, dividend.FindProperty("AccountId")!.GetContainingForeignKeys().Single().DeleteBehavior);
        Assert.Equal("CurrencyAccountEntryFinancialLabel", cash.FindSkipNavigation("Labels")!.JoinEntityType.GetTableName());
        Assert.Equal("InvestmentTransactionFinancialLabel", trade.FindSkipNavigation("Labels")!.JoinEntityType.GetTableName());
        Assert.Equal("BondAccountEntryEntryId", bond.FindNavigation("Labels")!.ForeignKey.Properties.Single().Name);
        Assert.Null(dividend.FindNavigation("Labels"));

        foreach (var (type, table, precision, scale) in new[] {
            (typeof(CurrencyAccountEntry), "CurrencyEntries", 18, 2),
            (typeof(BondAccountEntry), "BondEntries", 18, 2),
            (typeof(InvestmentTransaction), "InvestmentTransactions", 18, 8) })
        {
            var entity = context.Model.FindEntityType(type)!;
            Assert.Null(entity.BaseType);
            Assert.Null(entity.FindDiscriminatorProperty());
            Assert.Equal(table, entity.GetTableName());
            var value = entity.FindProperty("Value")!;
            Assert.Equal("_value", value.FieldInfo?.Name);
            Assert.Equal(PropertyAccessMode.PreferField, value.GetPropertyAccessMode());
            Assert.Equal(precision, value.GetPrecision());
            Assert.Equal(scale, value.GetScale());
            var postingDate = entity.FindProperty("PostingDate")!;
            Assert.Equal("<PostingDate>k__BackingField", postingDate.FieldInfo?.Name);
            Assert.Equal(PropertyAccessMode.PreferField, postingDate.GetPropertyAccessMode());
        }
    }

    [Fact]
    public void CurrencyAccount_PreservesExistingJson()
    {
        var date = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var model = new CurrencyAccount(2, 3, "Cash", [new CurrencyAccountEntry(3, 5_000_000_000L, date, 10m, 2m)], currencyId: 7);
        Assert.Equal("""{"accountType":6,"currencyId":7,"start":"2026-05-17T00:00:00Z","end":"2026-05-17T00:00:00Z","entries":[{"description":"","contractorDetails":null,"entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[]}],"containsAssets":true,"accountId":3,"userId":2,"name":"Cash"}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void BondAccount_PreservesExistingJson()
    {
        var date = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var model = new BondAccount(2, 3, "Bonds", [new BondAccountEntry(3, 5_000_000_000L, date, 10m, 2m, 4)]);
        Assert.Equal("""{"accountType":6,"start":"2026-05-17T00:00:00Z","end":"2026-05-17T00:00:00Z","entries":[{"bondDetailsId":4,"description":"","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[]}],"containsAssets":true,"accountId":3,"userId":2,"name":"Bonds"}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void InvestmentAccount_PreservesExistingJson()
    {
        var model = new InvestmentAccount(2, 3, "Broker");
        Assert.Equal("""{"accountId":3,"userId":2,"name":"Broker","start":null,"end":null}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void CurrencyAccountEntry_PreservesExistingJson()
    {
        var date = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var model = new CurrencyAccountEntry(3, 5_000_000_000L, date, 10m, 2m) { Description = "d", ContractorDetails = "c", UserId = -2, CurrencyId = 0 };
        Assert.Equal(model.UserId, model.GetCopy().UserId);
        Assert.Equal(model.CurrencyId, model.GetCopy().CurrencyId);
        Assert.Equal("""{"description":"d","contractorDetails":"c","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[]}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void BondAccountEntry_PreservesExistingJson()
    {
        var date = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var model = new BondAccountEntry(3, 5_000_000_000L, date, 10m, 2m, 4) { Description = "d", UserId = -2 };
        Assert.Equal(model.UserId, model.GetCopy().UserId);
        Assert.Equal("""{"bondDetailsId":4,"description":"d","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[]}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void InvestmentTransaction_PreservesExistingJson()
    {
        var date = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var model = new InvestmentTransaction { AccountId = 3, UserId = 2, AssetListingId = 4, TradeDate = new DateOnly(2026, 5, 17), Currency = "USD", Quantity = 2, Description = "d" };
        Assert.Equal("""{"description":"d","userId":2,"assetListingId":4,"assetListing":null,"type":0,"quantity":2,"unitPrice":0,"currency":"USD","fee":null,"createdAt":"0001-01-01T00:00:00+00:00","updatedAt":"0001-01-01T00:00:00+00:00","signedQuantity":2,"entryId":0,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","labels":[]}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void CurrencyAccountDto_PreservesExistingJson()
    {
        var model = new CurrencyAccountDto { AccountId = 3, UserId = 2, Name = "Cash", CurrencyId = 7 };
        Assert.Equal("""{"nextOlderEntry":null,"nextYoungerEntry":null,"entries":[],"accountType":0,"accountLabel":0,"accountId":3,"userId":2,"name":"Cash","currencyId":7}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void BondAccountDto_PreservesExistingJson()
    {
        var model = new BondAccountDto { AccountId = 3, UserId = 2, Name = "Bonds" };
        Assert.Equal("""{"nextOlderEntries":null,"nextYoungerEntries":null,"entries":[],"accountType":0,"accountLabel":0,"accountId":3,"userId":2,"name":"Bonds","currencyId":0}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void CurrencyAccountEntryDto_PreservesExistingJson()
    {
        var date = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var model = new CurrencyAccountEntryDto { AccountId = 3, EntryId = 5_000_000_000L, PostingDate = date, Value = 10m, ValueChange = 2m, Description = "d", ContractorDetails = "c" };
        Assert.Equal("""{"description":"d","contractorDetails":"c","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[]}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void BondAccountEntryDto_PreservesExistingJson()
    {
        var date = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var model = new BondAccountEntryDto { AccountId = 3, EntryId = 5_000_000_000L, PostingDate = date, Value = 10m, ValueChange = 2m, Description = "d", BondDetailsId = 4 };
        Assert.Equal("""{"bondDetailsId":4,"description":"d","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[]}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public async Task InvestmentMaterialization_PreservesStoredTimeAndNormalizesKind(DateTimeKind kind)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var context = new AppDbContext(options);
        var transaction = new InvestmentTransaction { AccountId = 1, Currency = "USD" };
        context.InvestmentTransactions.Add(transaction);
        var storedDate = new DateTime(2026, 5, 17, 13, 42, 0, kind);
        // Simulate an existing stored value: EF currently uses the field, bypassing the public date setter.
        context.Entry(transaction).Property(entry => entry.PostingDate).CurrentValue = storedDate;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var loaded = await context.InvestmentTransactions.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DateTimeKind.Utc, loaded.PostingDate.Kind);
        Assert.Equal(storedDate.ToUniversalTime(), loaded.PostingDate);
        Assert.NotEqual(TimeSpan.Zero, loaded.PostingDate.TimeOfDay);
    }
}