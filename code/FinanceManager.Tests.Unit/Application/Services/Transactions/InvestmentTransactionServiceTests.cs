using FinanceManager.Application.FinancialAccounts.Investments.Discovery;
using FinanceManager.Application.FinancialAccounts.Investments.Transactions;
using FinanceManager.Application.Shared.Persistence;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Assets.Repositories;
using FinanceManager.Domain.Dashboard.Services;
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
    private readonly ILogger<InvestmentTransactionService> _logger =
        LoggerFactory.Create(_ => { }).CreateLogger<InvestmentTransactionService>();

    [Fact]
    public async Task AddAsync_ValidatesExternalInstrumentBeforeStartingAtomicOperation()
    {
        var instrument = new InstrumentDiscoveryResultDto
        {
            DisplayName = "CSPX",
            Ticker = "CSPX",
            ExchangeMic = "XLON",
            TradingCurrency = "USD",
            ProviderSymbol = "CSPX.LON",
            SecurityType = "ETF"
        };
        _searchService.Setup(x => x.ResolveExternalResultAsync("result", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instrument);
        _importService.Setup(x => x.ValidateForTransactionAsync(instrument, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InstrumentImportException("provider_unavailable", "Provider unavailable."));

        var service = CreateService();
        var request = new AddInvestmentTransactionRequest(
            1, null, InvestmentTransactionType.Buy, 1m, 100m, "USD", new DateOnly(2026, 1, 1),
            ExternalInstrumentResultId: "result");

        var exception = await Assert.ThrowsAsync<InvestmentTransactionCommandException>(() =>
            service.AddAsync(request, 91, TestContext.Current.CancellationToken));

        Assert.Equal("provider_unavailable", exception.Code);
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
        _searchService.Setup(x => x.ResolveExternalResultAsync("result", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instrument);
        _importService.Setup(x => x.ValidateForTransactionAsync(instrument, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _importService.Setup(x => x.ImportValidatedAsync(instrument, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportedInstrumentDto { AssetListingId = 42, Ticker = "VWCE", TradingCurrency = "EUR" });
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
        _importService.Verify(x => x.ValidateForTransactionAsync(instrument, It.IsAny<CancellationToken>()), Times.Once);
        _importService.Verify(x => x.ImportValidatedAsync(instrument, It.IsAny<CancellationToken>()), Times.Once);
    }

    private InvestmentTransactionService CreateService() => new(
        _listingRepository.Object,
        _transactionRepository.Object,
        _searchService.Object,
        _importService.Object,
        _atomicOperation.Object,
        _cacheInvalidator.Object,
        _logger);
}