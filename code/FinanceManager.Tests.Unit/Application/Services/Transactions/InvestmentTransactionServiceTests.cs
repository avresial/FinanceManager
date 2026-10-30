using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Application.Shared.Persistence;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Transactions;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InvestmentTransactionServiceTests
{
    private readonly Mock<IAssetListingRepository> _listingRepository = new();
    private readonly Mock<IInvestmentTransactionRepository> _transactionRepository = new();
    private readonly Mock<IInvestmentInstrumentSearchService> _searchService = new();
    private readonly Mock<IInstrumentImportService> _importService = new();
    private readonly Mock<IAtomicOperation> _atomicOperation = new();
    private readonly Mock<ICacheInvalidator> _cacheInvalidator = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly ILogger<InvestmentTransactionService> _logger =
        LoggerFactory.Create(_ => { }).CreateLogger<InvestmentTransactionService>();

    [Fact]
    public async Task AddAsync_ValidatesExternalInstrumentBeforeStartingAtomicOperation()
    {
        var instrument = new InstrumentDiscoveryResultDto
        {
            DisplayName = "Corporate Bond",
            Ticker = "BOND1",
            ExchangeMic = "XLON",
            TradingCurrency = "USD",
            SecurityType = "Bond"
        };
        _searchService.Setup(x => x.ResolveExternalResultAsync("result", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instrument);
        _importService.Setup(x => x.ValidateForTransactionAsync(instrument, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InstrumentImportException("instrument_import_failed", "Only stocks and ETFs can be added to an investment transaction."));

        var service = CreateService();
        var request = new AddInvestmentTransactionRequest(
            1, null, InvestmentTransactionType.Buy, 1m, 100m, "USD", new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: "result");

        var exception = await Assert.ThrowsAsync<InvestmentTransactionCommandException>(() =>
            service.AddAsync(request, 91, TestContext.Current.CancellationToken));

        Assert.Equal("instrument_import_failed", exception.Code);
        _atomicOperation.Verify(
            x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task<InvestmentTransactionDto>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _transactionRepository.Verify(
            x => x.Add(It.IsAny<InvestmentTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddAsync_WithExternalInstrument_ValidatesProviderDataOnceAndImportsInsideTheTransaction()
    {
        var instrument = new InstrumentDiscoveryResultDto
        {
            Source = "AlphaVantage",
            DisplayName = "Vanguard FTSE All-World UCITS ETF",
            Ticker = "VWCE",
            ExchangeMic = "XETR",
            ExchangeCode = "XETRA",
            ExchangeName = "Xetra",
            TradingCurrency = "EUR",
            ProviderSymbol = "VWCE.DEX",
            SecurityType = "ETF",
            Warnings = ["Identity was not confirmed by OpenFIGI; check the exchange and currency before saving."]
        };
        var resolved = new MarketDataSymbolResolution(MarketDataProvider.AlphaVantage, 100, "resolved", "VWCE.DEX", "XETRA", "EUR");
        _searchService.Setup(x => x.ResolveExternalResultAsync("result", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instrument);
        _importService.Setup(x => x.ValidateForTransactionAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidatedInstrumentImport(instrument, [resolved]));
        _importService.Setup(x => x.ImportValidatedAsync(It.Is<ValidatedInstrumentImport>(x => x.Instrument == instrument), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportedInstrumentDto
            {
                AssetListingId = 42,
                MarketDataSymbolId = 9,
                Ticker = "VWCE",
                TradingCurrency = "EUR",
                ProviderResults = [resolved]
            });
        _atomicOperation
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task<InvestmentTransactionDto>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<InvestmentTransactionDto>> operation, CancellationToken ct) => operation(ct));
        _transactionRepository
            .Setup(x => x.Add(It.IsAny<InvestmentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentTransaction transaction, CancellationToken _) => { transaction.Id = 7; return transaction; });
        _transactionRepository
            .Setup(x => x.Get(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvestmentTransaction
            {
                Id = 7,
                AccountId = 1,
                AssetListingId = 42,
                AssetListing = new AssetListing
                {
                    Id = 42,
                    Ticker = "VWCE",
                    ExchangeMic = "XETR",
                    TradingCurrency = "EUR",
                    MarketDataSymbols = [new MarketDataSymbol { Provider = MarketDataProvider.AlphaVantage, Symbol = "VWCE.DEX", IsEnabled = true }]
                },
                Type = InvestmentTransactionType.Buy,
                Quantity = 1m,
                UnitPrice = 100m,
                Currency = "EUR",
                TradeDate = new DateOnly(2026, 1, 1)
            });

        var request = new AddInvestmentTransactionRequest(
            1, null, InvestmentTransactionType.Buy, 1m, 100m, "EUR", new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: "result");
        var result = await CreateService().AddAsync(request, 91, TestContext.Current.CancellationToken);

        Assert.Equal(7, result.Id);
        Assert.Equal(42, result.AssetListingId);
        Assert.True(result.AutomaticPricesAvailable);
        _importService.Verify(x => x.ValidateForTransactionAsync(instrument, It.IsAny<CancellationToken>()), Times.Once);
        _importService.Verify(x => x.ImportValidatedAsync(It.Is<ValidatedInstrumentImport>(x => x.Instrument == instrument), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_WithExternalInstrumentMissingVenueCurrencyAndPrices_SavesTransactionAndFlagsAutomaticPricesUnavailable()
    {
        var incomplete = new InstrumentDiscoveryResultDto
        {
            Source = "OpenFIGI",
            DisplayName = "VANG FTSE AW USDA",
            Ticker = "VWCE",
            ExchangeMic = null,
            ExchangeCode = "GF",
            TradingCurrency = null,
            SecurityType = null
        };
        _searchService.Setup(x => x.ResolveExternalResultAsync("incomplete-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(incomplete);
        _importService.Setup(x => x.ValidateForTransactionAsync(incomplete, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidatedInstrumentImport(incomplete, []));
        _importService.Setup(x => x.ImportValidatedAsync(It.Is<ValidatedInstrumentImport>(x => x.Instrument == incomplete), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportedInstrumentDto
            {
                AssetId = 15,
                AssetListingId = 55,
                MarketDataSymbolId = null,
                Ticker = "VWCE",
                ExchangeName = "GF",
                TradingCurrency = string.Empty,
                CreatedAsset = true,
                CreatedListing = true,
                CreatedMarketDataSymbol = false,
                ProviderResults = []
            });
        _atomicOperation
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task<InvestmentTransactionDto>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<InvestmentTransactionDto>> operation, CancellationToken ct) => operation(ct));
        _transactionRepository
            .Setup(x => x.Add(It.IsAny<InvestmentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentTransaction transaction, CancellationToken _) => { transaction.Id = 88; return transaction; });
        _transactionRepository
            .Setup(x => x.Get(88, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvestmentTransaction
            {
                Id = 88,
                AccountId = 1,
                AssetListingId = 55,
                AssetListing = new AssetListing
                {
                    Id = 55,
                    Ticker = "VWCE",
                    ExchangeMic = null,
                    ExchangeName = "GF",
                    TradingCurrency = null,
                    MarketDataSymbols = []
                },
                Type = InvestmentTransactionType.Buy,
                Quantity = 3m,
                UnitPrice = 118.45m,
                Currency = "EUR",
                TradeDate = new DateOnly(2026, 2, 10)
            });

        var request = new AddInvestmentTransactionRequest(
            1, null, InvestmentTransactionType.Buy, 3m, 118.45m, "eur", new DateOnly(2026, 2, 10),
            ExternalInstrumentResultId: "incomplete-id");
        var result = await CreateService().AddAsync(request, 91, TestContext.Current.CancellationToken);

        Assert.Equal(88, result.Id);
        Assert.Equal(55, result.AssetListingId);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal(118.45m, result.UnitPrice);
        Assert.False(result.AutomaticPricesAvailable);
    }

    [Fact]
    public async Task AddAsync_WhenKnownTradingCurrencyDoesNotMatchRequest_ThrowsCurrencyMismatch()
    {
        var instrument = new InstrumentDiscoveryResultDto
        {
            DisplayName = "CSPX",
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            TradingCurrency = "USD",
            SecurityType = "ETF"
        };
        _searchService.Setup(x => x.ResolveExternalResultAsync("result", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instrument);

        var request = new AddInvestmentTransactionRequest(
            1, null, InvestmentTransactionType.Buy, 1m, 100m, "PLN", new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: "result");

        var ex = await Assert.ThrowsAsync<InvestmentTransactionCommandException>(() =>
            CreateService().AddAsync(request, 91, TestContext.Current.CancellationToken));

        Assert.Equal("currency_mismatch", ex.Code);
        _importService.Verify(x => x.ValidateForTransactionAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_WhenTradingCurrencyUnknownAndRequestCurrencyInvalid_ThrowsInvalidCurrency()
    {
        var instrument = new InstrumentDiscoveryResultDto
        {
            DisplayName = "VWCE",
            Ticker = "VWCE",
            ExchangeMic = null,
            TradingCurrency = null
        };
        _searchService.Setup(x => x.ResolveExternalResultAsync("result", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instrument);

        var request = new AddInvestmentTransactionRequest(
            1, null, InvestmentTransactionType.Buy, 1m, 100m, "ZZZ", new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: "result");

        var ex = await Assert.ThrowsAsync<InvestmentTransactionCommandException>(() =>
            CreateService().AddAsync(request, 91, TestContext.Current.CancellationToken));

        Assert.Equal("invalid_currency", ex.Code);
        _importService.Verify(x => x.ValidateForTransactionAsync(It.IsAny<InstrumentDiscoveryResultDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddAsync_WithExistingIncompleteListing_AllowsPredefinedCurrencyAndPreservesNullListingCurrency()
    {
        var existingIncomplete = new AssetListing
        {
            Id = 77,
            Ticker = "VWCE",
            ExchangeMic = null,
            ExchangeName = "GF",
            TradingCurrency = null,
            MarketDataSymbols = []
        };
        _listingRepository.Setup(x => x.Get(77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIncomplete);
        _atomicOperation
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task<InvestmentTransactionDto>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<InvestmentTransactionDto>> operation, CancellationToken ct) => operation(ct));
        _transactionRepository
            .Setup(x => x.Add(It.IsAny<InvestmentTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvestmentTransaction transaction, CancellationToken _) => { transaction.Id = 99; return transaction; });
        _transactionRepository
            .Setup(x => x.Get(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvestmentTransaction
            {
                Id = 99,
                AccountId = 1,
                AssetListingId = 77,
                AssetListing = existingIncomplete,
                Type = InvestmentTransactionType.Buy,
                Quantity = 2m,
                UnitPrice = 450m,
                Currency = "PLN",
                TradeDate = new DateOnly(2026, 3, 1)
            });

        var request = new AddInvestmentTransactionRequest(
            1, 77, InvestmentTransactionType.Buy, 2m, 450m, "PLN", new DateOnly(2026, 3, 1));
        var result = await CreateService().AddAsync(request, 91, TestContext.Current.CancellationToken);

        Assert.Equal(99, result.Id);
        Assert.Equal("PLN", result.Currency);
        Assert.Null(existingIncomplete.TradingCurrency);
        Assert.False(result.AutomaticPricesAvailable);
    }

    private InvestmentTransactionService CreateService() => new(
        _listingRepository.Object,
        _transactionRepository.Object,
        _searchService.Object,
        _importService.Object,
        _atomicOperation.Object,
        _cacheInvalidator.Object,
        _currencyRepository.Object,
        _logger);
}