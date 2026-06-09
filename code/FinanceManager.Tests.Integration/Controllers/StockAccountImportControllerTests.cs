using FinanceManager.Application.Services.Stocks;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class StockAccountImportControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 501;
    private const int _otherUserId = 502;
    private const int _testAccountId = 1501;
    private TestDatabase? _testDatabase;
    private Mock<IStockAccountImportService>? _importServiceMock;

    protected override void ConfigureServices(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        _importServiceMock = new Mock<IStockAccountImportService>();
        _importServiceMock
            .Setup(x => x.ImportEntries(_testUserId, _testAccountId, It.IsAny<IEnumerable<StockEntryImport>>()))
            .ReturnsAsync(new StockImportResult(_testAccountId, 1, 0, [], []));
        services.AddSingleton(_importServiceMock.Object);
    }

    private async Task SeedAccount(int userId = _testUserId)
    {
        _testDatabase!.Context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = _testAccountId,
            UserId = userId,
            Name = "Imported Stocks",
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ImportStockEntries_ForOwnedAccount_ReturnsImportResult()
    {
        await SeedAccount();
        Authorize("user", _testUserId, UserRole.User);
        var dto = new StockDataImportDto(_testAccountId, [new(DateTime.UtcNow.Date, 25m, "AAPL")]);

        var response = await Client.PostAsJsonAsync("api/StockAccountImport/ImportStockEntries", dto, TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains($"\"accountId\":{_testAccountId}", content, StringComparison.OrdinalIgnoreCase);
        _importServiceMock!.Verify(x => x.ImportEntries(_testUserId, _testAccountId, It.IsAny<IEnumerable<StockEntryImport>>()), Times.Once);
    }

    [Fact]
    public async Task ImportStockEntries_ForOtherUsersAccount_ReturnsForbidden()
    {
        await SeedAccount(_otherUserId);
        Authorize("user", _testUserId, UserRole.User);
        var dto = new StockDataImportDto(_testAccountId, [new(DateTime.UtcNow.Date, 25m, "AAPL")]);

        var response = await Client.PostAsJsonAsync("api/StockAccountImport/ImportStockEntries", dto, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _importServiceMock!.Verify(x => x.ImportEntries(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<StockEntryImport>>()), Times.Never);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}