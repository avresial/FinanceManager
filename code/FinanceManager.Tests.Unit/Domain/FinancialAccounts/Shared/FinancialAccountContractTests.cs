using FinanceManager.Api.Features.FinancialAccounts.Bond.Extensions;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Extensions;
using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Domain.FinancialAccounts.Shared;

public class FinancialAccountContractTests
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);
    private static readonly DateTime _date = new(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WireEnumsRetainDomainNamesAndNumericValues()
    {
        AssertEnumParity<AccountType, FinanceManager.Contracts.FinancialAccounts.Shared.AccountTypeDto>();
        AssertEnumParity<AccountLabel, FinanceManager.Contracts.FinancialAccounts.Shared.AccountLabelDto>();
        AssertEnumParity<InvestmentTransactionType, InvestmentTransactionTypeDto>();
        AssertEnumParity<FinanceManager.Domain.Assets.Entities.AssetType, FinanceManager.Contracts.Assets.AssetTypeDto>();
    }

    private static void AssertEnumParity<TDomain, TContract>() where TDomain : struct, Enum where TContract : struct, Enum
    {
        Assert.Equal(Enum.GetNames<TDomain>(), Enum.GetNames<TContract>());
        Assert.Equal(Enum.GetValues<TDomain>().Select(value => Convert.ToInt32(value)),
            Enum.GetValues<TContract>().Select(value => Convert.ToInt32(value)));
    }

    private static FinancialLabel Label() => new()
    {
        Id = 9,
        Name = "Salary",
        Classifications = [new() { Id = 10, LabelId = 9, Kind = "income", Value = "regular" }]
    };

    [Fact]
    public void AccountMappersPreserveDefaultTypeAndStripEntryClassifications()
    {
        var currencyEntry = new CurrencyAccountEntry(3, 5_000_000_000L, _date, 10m, 2m)
        { Description = "d", ContractorDetails = "c", Labels = [Label()] };
        var bondEntry = new BondAccountEntry(3, 5_000_000_000L, _date, 10m, 2m, 4)
        { Description = "d", Labels = [Label()] };
        var currency = new CurrencyAccount(2, 3, "Cash", [currencyEntry], currencyId: 7).ToDto();
        var bond = new BondAccount(2, 3, "Bonds", [bondEntry]).ToDto(nextOlderEntries: new() { [4] = bondEntry });

        Assert.Equal("""{"nextOlderEntry":null,"nextYoungerEntry":null,"entries":[{"description":"d","contractorDetails":"c","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[{"id":9,"name":"Salary","classifications":[]}]}],"accountType":0,"accountLabel":6,"accountId":3,"userId":2,"name":"Cash","currencyId":7}""", JsonSerializer.Serialize(currency, _options));
        Assert.Equal("""{"nextOlderEntries":{"4":{"bondDetailsId":4,"description":"d","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[{"id":9,"name":"Salary","classifications":[]}]}},"nextYoungerEntries":{},"entries":[{"bondDetailsId":4,"description":"d","entryId":5000000000,"accountId":3,"postingDate":"2026-05-17T00:00:00Z","value":10,"valueChange":2,"labels":[{"id":9,"name":"Salary","classifications":[]}]}],"accountType":0,"accountLabel":6,"accountId":3,"userId":2,"name":"Bonds","currencyId":0}""", JsonSerializer.Serialize(bond, _options));
    }

    [Fact]
    public void InvestmentMappingPreservesPopulatedClassificationsAndLongIdentity()
    {
        var transaction = new InvestmentTransaction
        {
            EntryId = 5_000_000_000L,
            UserId = 5_000_000_002L,
            AccountId = 3,
            AssetListingId = 5_000_000_001L,
            Type = InvestmentTransactionType.Sell,
            Quantity = 2m,
            UnitPrice = 10m,
            Currency = "USD",
            TradeDate = DateOnly.FromDateTime(_date),
            Description = "d",
            Labels = [Label()]
        };
        var dto = transaction.ToDto();
        Assert.Equal("""{"id":5000000000,"userId":5000000002,"accountId":3,"assetListingId":5000000001,"type":1,"quantity":2,"unitPrice":10,"currency":"USD","tradeDate":"2026-05-17","fee":null,"notes":"d","ticker":"","exchangeName":"","assetType":6,"automaticPricesAvailable":true,"labels":[{"id":9,"name":"Salary","classifications":[{"id":10,"labelId":9,"kind":"income","value":"regular"}]}]}""", JsonSerializer.Serialize(dto, _options));
        var restored = JsonSerializer.Deserialize<InvestmentTransactionDto>(JsonSerializer.Serialize(dto, _options), _options)!;
        Assert.Equal(5_000_000_000L, restored.Id);
        Assert.Equal("regular", Assert.Single(Assert.Single(restored.Labels!).Classifications).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestsPreserveOptionalDefaultsAndNumericTypes(bool update)
    {
        const string fields = """
            "accountId":3,"assetListingId":5000000001,"type":1,"quantity":2,"unitPrice":10,"currency":"USD","tradeDate":"2026-05-17"
            """;
        if (update)
        {
            var request = JsonSerializer.Deserialize<UpdateInvestmentTransactionRequest>("{\"id\":5000000000," + fields + "}", _options)!;
            Assert.Null(request.Fee);
            Assert.Null(request.Notes);
            Assert.Null(request.LabelIds);
            Assert.Equal(InvestmentTransactionTypeDto.Sell, request.Type);
            Assert.Equal(5_000_000_000L, request.ToEntity().EntryId);
            Assert.Equal("""{"id":5000000000,"accountId":3,"assetListingId":5000000001,"type":1,"quantity":2,"unitPrice":10,"currency":"USD","tradeDate":"2026-05-17","fee":null,"notes":null,"labelIds":[]}""", JsonSerializer.Serialize(request with { LabelIds = [] }, _options));
        }
        else
        {
            var request = JsonSerializer.Deserialize<AddInvestmentTransactionRequest>("{" + fields + "}", _options)!;
            Assert.Null(request.Fee);
            Assert.Null(request.Notes);
            Assert.Null(request.ExternalInstrumentResultId);
            Assert.Null(request.LabelIds);
            Assert.Equal(InvestmentTransactionType.Sell, request.ToEntity(2, 5_000_000_001L).Type);
            Assert.Equal("""{"accountId":3,"assetListingId":5000000001,"type":1,"quantity":2,"unitPrice":10,"currency":"USD","tradeDate":"2026-05-17","fee":null,"notes":null,"externalInstrumentResultId":null,"labelIds":[9]}""", JsonSerializer.Serialize(request with { LabelIds = [9] }, _options));
        }
    }

    [Fact]
    public void UndefinedEnumValuesAndLabelUpdateSemanticsRemainUnchanged()
    {
        var request = new UpdateInvestmentTransactionRequest(5_000_000_000L, 3, 4, (InvestmentTransactionTypeDto)17,
            2m, 10m, "USD", DateOnly.FromDateTime(_date), LabelIds: [9, 9]);
        var entity = request.ToEntity();
        Assert.Equal((InvestmentTransactionType)17, entity.Type);
        Assert.Equal((InvestmentTransactionTypeDto)17, entity.ToDto().Type);
        Assert.Equal(9, Assert.Single(entity.Labels).Id);
        Assert.Equal(InvestmentTransactionTypeDto.Buy, new AddInvestmentTransactionRequest(3, 4, InvestmentTransactionTypeDto.Buy,
            1m, 10m, "USD", DateOnly.FromDateTime(_date)).Type);
    }

    [Fact]
    public void NullLabelsAndContinuationPreserveStoredSnapshotShape()
    {
        var transaction = new InvestmentTransactionDto(5_000_000_000L, 5_000_000_002L, 3, 4, InvestmentTransactionTypeDto.Buy,
            2m, 10m, "USD", DateOnly.FromDateTime(_date), null, null);
        var page = new InvestmentTransactionHistoryPageDto([transaction], false);
        Assert.Equal("""{"items":[{"id":5000000000,"userId":5000000002,"accountId":3,"assetListingId":4,"type":0,"quantity":2,"unitPrice":10,"currency":"USD","tradeDate":"2026-05-17","fee":null,"notes":null,"ticker":"","exchangeName":"","assetType":6,"automaticPricesAvailable":true,"labels":null}],"hasMore":false,"nextCursor":null}""", JsonSerializer.Serialize(page, _options));
    }
}