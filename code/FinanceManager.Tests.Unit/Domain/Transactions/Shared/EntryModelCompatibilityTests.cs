using FinanceManager.Domain.Portfolio.Dividends.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Investments.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Domain.Transactions.Shared;

[Trait("Category", "Unit")]
public class EntryModelCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RelationalModelsRetainConcreteTablesAndFieldAccess(bool sqlServer)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer) builder.UseSqlServer("Server=localhost;Database=model-only;TrustServerCertificate=true");
        else builder.UseNpgsql("Host=localhost;Database=model-only");
        using var context = new AppDbContext(builder.Options);

        var cash = context.Model.FindEntityType(typeof(CashEntry))!;
        var bond = context.Model.FindEntityType(typeof(BondEntry))!;
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

        // Physical names that predate the CashEntry and BondEntry type names and must not follow them.
        var cashJoin = cash.FindSkipNavigation("Labels")!.JoinEntityType;
        Assert.Equal("CurrencyAccountEntryFinancialLabel", cashJoin.Name);
        Assert.Equal(["CurrencyAccountEntryEntryId", "LabelsId"], cashJoin.GetProperties().Select(property => property.GetColumnName()).Order());
        Assert.Equal(["CurrencyAccountEntryEntryId", "LabelsId"], cashJoin.FindPrimaryKey()!.Properties.Select(property => property.Name));
        var bondLabelColumn = bond.FindNavigation("Labels")!.ForeignKey.Properties.Single();
        Assert.Equal("FinancialLabels", bondLabelColumn.DeclaringType.GetTableName());
        Assert.Equal("BondAccountEntryEntryId", bondLabelColumn.GetColumnName());

        foreach (var (type, table, precision, scale) in new[] {
            (typeof(CashEntry), "CurrencyEntries", 18, 2),
            (typeof(BondEntry), "BondEntries", 18, 2),
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
    public void InvestmentTransaction_PreservesExistingJson()
    {
        var date = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var model = new InvestmentTransaction { AccountId = 3, UserId = 2, AssetListingId = 4, TradeDate = new DateOnly(2026, 5, 17), Currency = "USD", Quantity = 2, Description = "d" };
        Assert.Equal("""{"description":"d","userId":2,"assetListingId":4,"assetListing":null,"type":0,"quantity":2,"unitPrice":0,"currency":"USD","fee":null,"createdAt":"0001-01-01T00:00:00+00:00","updatedAt":"0001-01-01T00:00:00+00:00","signedQuantity":2,"entryId":0,"postingDate":"2026-05-17T00:00:00Z","labels":[]}""", JsonSerializer.Serialize(model, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
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