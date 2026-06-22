using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class InvestmentTransactionControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 91;
    private const int _testAccountId = 791;
    private const long _listingId = 5001;
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        var planVerifierMock = new Mock<IUserPlanVerifier>();
        planVerifierMock.Setup(x => x.CanAddMoreAccounts(_testUserId)).ReturnsAsync(true);
        planVerifierMock.Setup(x => x.CanAddMoreEntries(_testUserId, It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(planVerifierMock.Object);
    }

    private async Task SeedAccount()
    {
        if (_testDatabase is null) return;
        if (await _testDatabase.Context.Accounts.AnyAsync(a => a.AccountId == _testAccountId, TestContext.Current.CancellationToken)) return;
        _testDatabase.Context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = _testAccountId,
            UserId = _testUserId,
            Name = "Test Investment Account",
            AccountLabel = AccountLabel.Stock,
            AccountType = AccountType.Stock
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<long> SeedTransaction(InvestmentTransactionType type, decimal quantity, DateOnly tradeDate)
    {
        await SeedAccount();
        var transaction = new InvestmentTransaction
        {
            UserId = _testUserId,
            AccountId = _testAccountId,
            AssetListingId = _listingId,
            Type = type,
            Quantity = quantity,
            UnitPrice = 100m,
            Currency = "USD",
            TradeDate = tradeDate
        };
        _testDatabase!.Context.InvestmentTransactions.Add(transaction);
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return transaction.Id;
    }

    private static AddInvestmentTransactionRequest AddRequest(int accountId) => new(
        accountId, _listingId, InvestmentTransactionType.Buy, 3m, 120m, "USD", new DateOnly(2024, 6, 1));

    [Fact]
    public async Task Add_CreatesTransaction()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var result = await client.AddAsync(AddRequest(_testAccountId));

        Assert.NotNull(result);
        Assert.Equal(_testUserId, result!.UserId);
        Assert.Equal(3m, result.Quantity);

        var inDb = await _testDatabase!.Context.InvestmentTransactions
            .FirstOrDefaultAsync(x => x.AccountId == _testAccountId, TestContext.Current.CancellationToken);
        Assert.NotNull(inDb);
        Assert.Equal(120m, inDb!.UnitPrice);
        Assert.Equal(_testUserId, inDb.UserId);
    }

    [Fact]
    public async Task Add_ForOtherUsersAccount_ReturnsForbidden()
    {
        await SeedAccount();
        Authorize("otheruser", _testUserId + 1, UserRole.User);

        var response = await Client.PostAsJsonAsync("api/InvestmentTransaction/Add", AddRequest(_testAccountId), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Add_WithInvalidQuantity_ReturnsBadRequest()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var invalid = AddRequest(_testAccountId) with { Quantity = 0m };

        var response = await Client.PostAsJsonAsync("api/InvestmentTransaction/Add", invalid, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetByAccount_ReturnsTransactions()
    {
        await SeedTransaction(InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10));
        await SeedTransaction(InvestmentTransactionType.Sell, 2m, new DateOnly(2024, 2, 10));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var transactions = await client.GetByAccountAsync(_testAccountId);

        Assert.Equal(2, transactions.Count);
    }

    [Fact]
    public async Task Get_ReturnsTransaction()
    {
        var id = await SeedTransaction(InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var result = await client.GetAsync(id);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id);
        Assert.Equal(_testAccountId, result.AccountId);
    }

    [Fact]
    public async Task Update_ForUnknownTransaction_ReturnsFalse()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);
        var update = new UpdateInvestmentTransactionRequest(
            long.MaxValue, _testAccountId, _listingId, InvestmentTransactionType.Buy, 8m, 130m, "USD", new DateOnly(2024, 1, 10));

        var result = await client.UpdateAsync(update);

        Assert.False(result);
    }

    [Fact]
    public async Task Delete_ForUnknownTransaction_ReturnsFalse()
    {
        await SeedAccount();
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var result = await client.DeleteAsync(_testAccountId, long.MaxValue);

        Assert.False(result);
    }

    [Fact]
    public async Task Update_ModifiesTransaction()
    {
        var id = await SeedTransaction(InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);
        var update = new UpdateInvestmentTransactionRequest(
            id, _testAccountId, _listingId, InvestmentTransactionType.Buy, 8m, 130m, "USD", new DateOnly(2024, 1, 10));

        var result = await client.UpdateAsync(update);

        Assert.True(result);
        var inDb = await _testDatabase!.Context.InvestmentTransactions
            .FirstOrDefaultAsync(x => x.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(8m, inDb!.Quantity);
        Assert.Equal(130m, inDb.UnitPrice);
    }

    [Fact]
    public async Task Delete_RemovesTransaction()
    {
        var id = await SeedTransaction(InvestmentTransactionType.Buy, 5m, new DateOnly(2024, 1, 10));
        Authorize("testuser", _testUserId, UserRole.User);
        var client = new InvestmentTransactionHttpClient(Client);

        var result = await client.DeleteAsync(_testAccountId, id);

        Assert.True(result);
        var inDb = await _testDatabase!.Context.InvestmentTransactions
            .FirstOrDefaultAsync(x => x.Id == id, TestContext.Current.CancellationToken);
        Assert.Null(inDb);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}