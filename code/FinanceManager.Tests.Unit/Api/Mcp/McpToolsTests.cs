using FinanceManager.Api.Mcp;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using Microsoft.AspNetCore.Http;
using Moq;
using OpenIddict.Abstractions;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Unit.Api.Mcp;

public sealed class McpToolsTests
{
    private const int _userId = 73;

    [Fact]
    public void UserContextAndIdentity_UseValidatedOAuthSubject()
    {
        var context = UserContext(
            new Claim(OpenIddictConstants.Claims.Subject, _userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, "999"),
            new Claim(OpenIddictConstants.Claims.Name, "mcp-user"),
            new Claim(OpenIddictConstants.Claims.Role, "User"));

        var identity = new IdentityTools(context).WhoAmI();

        Assert.Equal(_userId, context.GetUserId());
        Assert.Equal(_userId.ToString(), identity.UserId);
        Assert.Equal("mcp-user", identity.Login);
        Assert.Equal(["User"], identity.Roles);
    }

    [Fact]
    public void UserContext_RejectsMissingOrInvalidSubject()
    {
        Assert.Throws<InvalidOperationException>(() => UserContext().GetUserId());
        Assert.Throws<InvalidOperationException>(() =>
            UserContext(new Claim(OpenIddictConstants.Claims.Subject, "not-a-user-id")).GetUserId());
    }

    [Fact]
    public async Task FinancialAccounts_UseSubjectAndFilterForeignRepositoryRows()
    {
        var currencies = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        var bonds = new Mock<IAccountRepository<BondAccount>>();
        var investments = new Mock<IAccountRepository<InvestmentAccount>>();
        currencies.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([
            new CurrencyAccount(_userId, 1, "Cash", AccountLabel.Cash),
            new CurrencyAccount(999, 2, "Foreign cash", AccountLabel.Cash)
        ]);
        bonds.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([]);
        investments.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([]);
        var tools = new FinancialAccountTools(UserContext(Subject()), currencies.Object, bonds.Object, investments.Object);

        var result = await tools.ListFinancialAccounts();

        var account = Assert.Single(result);
        Assert.Equal(1, account.AccountId);
        Assert.Equal("Cash", account.Name);
        Assert.Equal("currency", account.Type);
        currencies.Verify(repository => repository.GetAll(_userId), Times.Once);
        bonds.Verify(repository => repository.GetAll(_userId), Times.Once);
        investments.Verify(repository => repository.GetAll(_userId), Times.Once);
    }

    [Fact]
    public async Task Transactions_ReturnEmptyForForeignAccountWithoutReadingItsEntries()
    {
        var currencies = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        var bonds = new Mock<IAccountRepository<BondAccount>>();
        var investments = new Mock<IAccountRepository<InvestmentAccount>>();
        var currencyEntries = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        var bondEntries = new Mock<IBondAccountEntryRepository<BondAccountEntry>>();
        var investmentTransactions = new Mock<IInvestmentTransactionRepository>();
        var bondDetails = new Mock<IBondDetailsRepository>();
        currencies.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([
            new CurrencyAccount(_userId, 1, "Cash", AccountLabel.Cash)
        ]);
        bonds.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([]);
        investments.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([]);
        var tools = new TransactionTools(
            UserContext(Subject()), currencies.Object, bonds.Object, investments.Object,
            currencyEntries.Object, bondEntries.Object, investmentTransactions.Object, bondDetails.Object);

        var result = await tools.ListTransactions(
            accountId: 999,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Transactions);
        currencyEntries.Verify(repository => repository.GetRange(
            It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        bondEntries.Verify(repository => repository.GetRange(
            It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
        investmentTransactions.Verify(repository => repository.GetByUser(
            It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Transactions_DiscardForeignRowsReturnedByRepository()
    {
        var currencies = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        var bonds = new Mock<IAccountRepository<BondAccount>>();
        var investments = new Mock<IAccountRepository<InvestmentAccount>>();
        var currencyEntries = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        var bondEntries = new Mock<IBondAccountEntryRepository<BondAccountEntry>>();
        var investmentTransactions = new Mock<IInvestmentTransactionRepository>();
        var bondDetails = new Mock<IBondDetailsRepository>();
        currencies.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([
            new CurrencyAccount(_userId, 1, "Cash", AccountLabel.Cash)
        ]);
        bonds.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([]);
        investments.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([]);
        currencyEntries.Setup(repository => repository.GetRange(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([new CurrencyAccountEntry(999, 17, DateTime.UtcNow, 10, -10)]);
        investmentTransactions.Setup(repository => repository.GetByUser(
                _userId, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var tools = new TransactionTools(
            UserContext(Subject()), currencies.Object, bonds.Object, investments.Object,
            currencyEntries.Object, bondEntries.Object, investmentTransactions.Object, bondDetails.Object);

        var result = await tools.ListTransactions(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Transactions);
    }

    [Fact]
    public async Task InvestmentPortfolio_UsesSubjectAndFiltersForeignRows()
    {
        var accounts = new Mock<IAccountRepository<InvestmentAccount>>();
        var valuation = new Mock<IInvestmentValuationService>();
        var currencies = new Mock<ICurrencyRepository>();
        var listings = new Mock<IAssetListingRepository>();
        accounts.Setup(repository => repository.GetAll(_userId)).ReturnsAsync([
            new InvestmentAccount(_userId, 10, "Broker"),
            new InvestmentAccount(999, 20, "Foreign broker")
        ]);
        currencies.Setup(repository => repository.GetByCode("PLN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultCurrency.PLN);
        valuation.Setup(service => service.GetAccountValueAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 10 })),
                DefaultCurrency.PLN,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, decimal> { [10] = 1200m });
        valuation.Setup(service => service.GetHoldingsAsOfAsync(
                10, new DateOnly(2026, 7, 21), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<long, decimal> { [100] = 3 });
        listings.Setup(repository => repository.Get(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssetListing(100, "OWN"));
        var tools = new InvestmentTools(UserContext(Subject()), accounts.Object, valuation.Object, currencies.Object, listings.Object);

        var result = await tools.GetInvestmentPortfolio(
            new DateOnly(2026, 7, 21),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1200m, result.TotalValue);
        Assert.Equal(10, Assert.Single(result.Accounts).AccountId);
        var position = Assert.Single(result.Positions);
        Assert.Equal("OWN", position.Ticker);
        Assert.Equal(3, position.Quantity);
        valuation.Verify(service => service.GetHoldingsAsOfAsync(
            10, new DateOnly(2026, 7, 21), It.IsAny<CancellationToken>()), Times.Once);
        valuation.Verify(service => service.GetHoldingsAsOfAsync(
            20, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReferenceData_RequiresIdentityAndMapsOnlySafeFields()
    {
        var currencies = new Mock<ICurrencyRepository>();
        var labels = new Mock<IFinancialLabelsRepository>();
        currencies.Setup(repository => repository.GetCurrencies(It.IsAny<CancellationToken>()))
            .Returns(new[] { DefaultCurrency.USD }.ToAsyncEnumerable());
        labels.Setup(repository => repository.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(new[]
            {
                new FinancialLabel
                {
                    Id = 4,
                    Name = "Salary",
                    Classifications = [new FinancialLabelClassification { Id = 88, LabelId = 4, Kind = "income", Value = "salary" }]
                }
            }.ToAsyncEnumerable());
        var tools = new ReferenceDataTools(UserContext(Subject()), currencies.Object, labels.Object);

        var result = await tools.ListReferenceData(TestContext.Current.CancellationToken);

        Assert.Equal("USD", Assert.Single(result.Currencies).Code);
        var label = Assert.Single(result.Labels);
        Assert.Equal("Salary", label.Name);
        var classification = Assert.Single(label.Classifications);
        Assert.Equal("income", classification.Kind);
        Assert.Null(classification.GetType().GetProperty("Id"));
        Assert.Null(classification.GetType().GetProperty("LabelId"));
    }

    [Fact]
    public void ResponseMappings_OmitOwnershipAndInternalPersistenceFields()
    {
        var label = new FinancialLabel { Id = 4, Name = "Food" };
        var currencyEntry = new CurrencyAccountEntry(1, 5, DateTime.UtcNow, 100, -20)
        {
            Description = "Lunch",
            ContractorDetails = "private raw counterparty",
            Labels = [label]
        };
        var investment = InvestmentTransaction(_userId, 10, 100, "SAFE", 2);
        investment.Notes = "private note";
        investment.CreatedAt = DateTimeOffset.UtcNow;
        investment.AssetListing.MarketDataSymbols = [new MarketDataSymbol { Symbol = "provider-secret" }];
        var payload = new object[]
        {
            McpResponseMapper.Account(new CurrencyAccount(_userId, 1, "Cash", AccountLabel.Cash)),
            McpResponseMapper.Transaction(currencyEntry, "Cash"),
            McpResponseMapper.Transaction(new CurrencyAccountEntry(1, 6, DateTime.UtcNow, 100, -20)
            {
                ContractorDetails = "fallback-sensitive counterparty"
            }, "Cash"),
            McpResponseMapper.InvestmentPosition(10, "Broker", investment.AssetListing, 2),
            McpResponseMapper.Currency(DefaultCurrency.USD),
            McpResponseMapper.Label(label)
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("Lunch", json, StringComparison.Ordinal);
        Assert.Contains("SAFE", json, StringComparison.Ordinal);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contractorDetails", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private raw counterparty", json, StringComparison.Ordinal);
        Assert.DoesNotContain("fallback-sensitive counterparty", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private note", json, StringComparison.Ordinal);
        Assert.DoesNotContain("createdAt", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider-secret", json, StringComparison.Ordinal);
    }

    private static McpUserContext UserContext(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "McpOAuth"))
        };
        return new McpUserContext(new HttpContextAccessor { HttpContext = httpContext });
    }

    private static Claim Subject() => new(OpenIddictConstants.Claims.Subject, _userId.ToString());

    private static AssetListing AssetListing(long id, string ticker) => new()
    {
        Id = id,
        Ticker = ticker,
        ExchangeMic = "XNYS",
        ExchangeName = "New York Stock Exchange",
        TradingCurrency = "USD"
    };

    private static InvestmentTransaction InvestmentTransaction(int userId, int accountId, long listingId, string ticker, decimal quantity) => new()
    {
        Id = listingId,
        UserId = userId,
        AccountId = accountId,
        AssetListingId = listingId,
        Type = InvestmentTransactionType.Buy,
        Quantity = quantity,
        UnitPrice = 100,
        Currency = "USD",
        TradeDate = new DateOnly(2026, 1, 1),
        AssetListing = AssetListing(listingId, ticker)
    };
}