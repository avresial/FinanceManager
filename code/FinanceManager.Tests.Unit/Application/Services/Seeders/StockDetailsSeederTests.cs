using FinanceManager.Application.FinancialAccounts.Stock.Market;
using FinanceManager.Application.FinancialAccounts.Stock.Seeders;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Seeders;

[Collection("Application")]
[Trait("Category", "Unit")]
public class StockDetailsSeederTests
{
    private readonly Mock<IStockDetailsRepository> _stockDetailsRepository = new();
    private readonly Mock<IStockMarketService> _stockMarketService = new();
    private readonly Mock<ILogger<StockDetailsSeeder>> _logger = new();

    [Fact]
    public async Task Seed_WhenDisabled_DoesNotQueryRepositoryOrApi()
    {
        // Arrange
        var seeder = CreateSeeder(enabled: false);

        // Act
        await seeder.Seed(TestContext.Current.CancellationToken);

        // Assert
        _stockDetailsRepository.Verify(r => r.GetAll(It.IsAny<CancellationToken>()), Times.Never);
        _stockMarketService.Verify(s => s.ListStockDetails(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_WhenEnabledAndExistingDetailsFound_SkipsApiImport()
    {
        // Arrange
        _stockDetailsRepository
            .Setup(r => r.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateDetails("US0378331005", "AAPL"),
                CreateDetails("US5949181045", "MSFT"),
                CreateDetails("US02079K3059", "GOOGL")
            ]);
        var seeder = CreateSeeder(enabled: true);

        // Act
        await seeder.Seed(TestContext.Current.CancellationToken);

        // Assert
        _stockMarketService.Verify(s => s.ListStockDetails(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Seed_WhenEnabledAndEmpty_AddsApiListings()
    {
        // Arrange
        var details = CreateDetails("US0378331005", "AAPL");
        _stockDetailsRepository
            .Setup(r => r.GetAll(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _stockMarketService
            .Setup(s => s.ListStockDetails(It.IsAny<CancellationToken>()))
            .ReturnsAsync([details]);
        _stockDetailsRepository
            .Setup(r => r.Add(details, It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        var seeder = CreateSeeder(enabled: true);

        // Act
        await seeder.Seed(TestContext.Current.CancellationToken);

        // Assert
        _stockDetailsRepository.Verify(r => r.Add(details, It.IsAny<CancellationToken>()), Times.Once);
    }

    private StockDetailsSeeder CreateSeeder(bool enabled)
        => new(
            _stockDetailsRepository.Object,
            _stockMarketService.Object,
            Options.Create(new StockDetailsSeederOptions { Enabled = enabled }),
            _logger.Object);

    private static StockDetails CreateDetails(string isin, string ticker) => new()
    {
        Isin = isin,
        Ticker = ticker,
        Name = ticker,
        Type = "Equity",
        Region = "United States",
        Currency = new Currency(1, "USD", "$")
    };
}