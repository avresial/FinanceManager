using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Dashboard.Controllers;

[Trait("Category", "Integration")]
public class TransactionLogControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _testUserId = 641;
    private static readonly Mock<ITransactionLogService> _serviceMock = new();

    protected override void ConfigureServices(IServiceCollection services)
    {
        _serviceMock.Reset();

        _serviceMock
            .Setup(x => x.GetLastTransactions(_testUserId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TransactionLogEntryDto(10, "Bank", AccountType.Currency, 1, new DateTime(2024, 3, 5), -50m, "Groceries")]);
        _serviceMock.Setup(x => x.GetPage(_testUserId, It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionLogPage(
                [new(10, "Bank", AccountType.Currency, 1, new DateTime(2024, 3, 5), -50m, "Groceries")],
                new(new DateTime(2024, 3, 5), AccountType.Currency, 1)));
        services.AddSingleton(_serviceMock.Object);
    }

    [Fact]
    public async Task Get_ForAuthenticatedUser_ReturnsTransactions()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/TransactionLog/Get/{_testUserId}?count=5", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Groceries", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_ForOtherUser_ReturnsForbidden()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync("api/TransactionLog/Get/999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _serviceMock.Verify(x => x.GetLastTransactions(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Page_ForOwner_BindsAndSerializesCompositeCursor()
    {
        Authorize("user", _testUserId, UserRole.User);
        var response = await Client.GetAsync($"api/TransactionLog/Page/{_testUserId}?start=2024-03-01&end=2024-03-06&pageSize=2&cursorDate=2024-03-05T12:00:00&cursorAccountType=Bond&cursorEntryId=9223372036854775807", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<TransactionLogPage>(TestContext.Current.CancellationToken);
        Assert.NotNull(page);
        Assert.Equal("Groceries", Assert.Single(page.Items).Description);
        Assert.Equal(new TransactionLogCursor(new DateTime(2024, 3, 5), AccountType.Currency, 1), page.NextCursor);
        _serviceMock.Verify(s => s.GetPage(_testUserId,
            new TransactionLogQuery(new DateTime(2024, 3, 1), new DateTime(2024, 3, 6), 2,
                new(new DateTime(2024, 3, 5, 12, 0, 0), AccountType.Bond, long.MaxValue)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("start=2024-03-01&end=2024-03-06&pageSize=0")]
    [InlineData("start=2024-03-01&end=2024-03-06&pageSize=101")]
    [InlineData("start=2024-03-07&end=2024-03-06")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorDate=2024-03-05")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorAccountType=Bond")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorEntryId=1")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorDate=2024-03-05&cursorAccountType=99&cursorEntryId=1")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorDate=2024-03-05&cursorAccountType=Bond&cursorEntryId=0")]
    [InlineData("start=not-a-date&end=2024-03-06")]
    [InlineData("end=2024-03-06")]
    [InlineData("start=2024-03-01")]
    public async Task Page_InvalidQuery_ReturnsBadRequest(string query)
    {
        Authorize("user", _testUserId, UserRole.User);
        var response = await Client.GetAsync($"api/TransactionLog/Page/{_testUserId}?{query}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _serviceMock.Verify(x => x.GetPage(It.IsAny<int>(), It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, 641, HttpStatusCode.Unauthorized)]
    [InlineData(true, 999, HttpStatusCode.Forbidden)]
    public async Task Page_RequiresAuthenticatedOwner(bool authenticated, int userId, HttpStatusCode status)
    {
        ClearAuthorization();
        if (authenticated)
            Authorize("user", _testUserId, UserRole.User);
        var response = await Client.GetAsync($"api/TransactionLog/Page/{userId}?start=2024-03-01&end=2024-03-06", TestContext.Current.CancellationToken);
        Assert.Equal(status, response.StatusCode);
        _serviceMock.Verify(x => x.GetPage(It.IsAny<int>(), It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}