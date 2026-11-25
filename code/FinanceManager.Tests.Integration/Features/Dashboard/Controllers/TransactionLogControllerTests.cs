using FinanceManager.Contracts.Dashboard;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
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
            .ReturnsAsync([new TransactionLogEntryDto(TransactionKind.Cash, 1, new DateTime(2024, 3, 5), -50m, "Groceries")]);
        _serviceMock.Setup(x => x.GetItemsPage(_testUserId, It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactionLogItemsPage(
                [new BondTransactionLogItem(new(long.MaxValue, new DateTime(2024, 3, 5), 1, "Bond", 7, "USD", null, []))],
                new(new DateTime(2024, 3, 5), TransactionKind.Bond, long.MaxValue)));
        _serviceMock.Setup(x => x.HasAny(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _serviceMock.Setup(x => x.HasAny(-_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        services.AddSingleton(_serviceMock.Object);
    }

    [Fact]
    public async Task ItemsPage_ForOwner_SerializesTheKindDiscriminatorAndCursor()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/TransactionLog/ItemsPage/{_testUserId}?start=2024-03-01&end=2024-03-06&pageSize=1&kinds=Bond",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"kind\":\"bond\"", json);
        var page = await response.Content.ReadFromJsonAsync<TransactionLogItemsPageDto>(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("\"value\":", json);
        Assert.DoesNotContain("\"accountName\":", json);
        var bond = Assert.IsType<BondTransactionLogItemDto>(Assert.Single(page!.Items));
        Assert.Equal((long.MaxValue, 7), (bond.EntryId, bond.BondDetailsId));
        Assert.Equal(new TransactionLogCursorDto(new DateTime(2024, 3, 5), TransactionKindDto.Bond, long.MaxValue), page.NextCursor);
        _serviceMock.Verify(s => s.GetItemsPage(_testUserId, It.Is<TransactionLogQuery>(q =>
            q.PageSize == 1 && q.Kinds!.SequenceEqual(new[] { TransactionKind.Bond })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("999", "start=2024-03-01&end=2024-03-06", HttpStatusCode.Forbidden)]
    [InlineData("641", "start=2024-03-07&end=2024-03-06", HttpStatusCode.BadRequest)]
    [InlineData("641", "start=2024-03-01&end=2024-03-06&cursorEntryId=1", HttpStatusCode.BadRequest)]
    public async Task ItemsPage_RejectsOtherUsersAndInvalidQueries(string userId, string query, HttpStatusCode expected)
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/TransactionLog/ItemsPage/{userId}?{query}", TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
        _serviceMock.Verify(x => x.GetItemsPage(It.IsAny<int>(), It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Theory]
    [InlineData(_testUserId, true)]
    [InlineData(-_testUserId, false)]
    public async Task Exists_ForTheOwnerIncludingAGuest_ReturnsTheBoolean(int userId, bool expected)
    {
        Authorize("user", userId, UserRole.User);

        var response = await Client.GetAsync($"api/TransactionLog/Exists/{userId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<bool>(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("999")]
    [InlineData("0")]
    public async Task Exists_ForAnotherUserOrZero_IsForbidden(string userId)
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync($"api/TransactionLog/Exists/{userId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _serviceMock.Verify(x => x.HasAny(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Exists_WithoutAuthentication_IsUnauthorized()
    {
        ClearAuthorization();
        var response = await Client.GetAsync($"api/TransactionLog/Exists/{_testUserId}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RetiredCompactPageHasNoApiAction()
    {
        Authorize("user", _testUserId, UserRole.User);
        var response = await Client.GetAsync($"api/TransactionLog/Page/{_testUserId}", TestContext.Current.CancellationToken);
        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        _serviceMock.VerifyNoOtherCalls();
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
        var response = await Client.GetAsync($"api/TransactionLog/ItemsPage/{_testUserId}?start=2024-03-01&end=2024-03-06&pageSize=2&cursorDate=2024-03-05T12:00:00&cursorKind=Bond&cursorEntryId=9223372036854775807", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _serviceMock.Verify(s => s.GetItemsPage(_testUserId,
            new TransactionLogQuery(new DateTime(2024, 3, 1), new DateTime(2024, 3, 6), 2,
                new(new DateTime(2024, 3, 5, 12, 0, 0), TransactionKind.Bond, long.MaxValue)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("start=2024-03-01&end=2024-03-06&pageSize=0")]
    [InlineData("start=2024-03-01&end=2024-03-06&pageSize=101")]
    [InlineData("start=2024-03-07&end=2024-03-06")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorDate=2024-03-05")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorKind=Bond")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorEntryId=1")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorDate=2024-03-05&cursorKind=99&cursorEntryId=1")]
    [InlineData("start=2024-03-01&end=2024-03-06&cursorDate=2024-03-05&cursorKind=Bond&cursorEntryId=0")]
    [InlineData("start=not-a-date&end=2024-03-06")]
    [InlineData("end=2024-03-06")]
    [InlineData("start=2024-03-01")]
    [InlineData("start=2024-03-01&end=2024-03-06&kinds=99")]
    [InlineData("start=2024-03-01&end=2024-03-06&kinds=Currency&kinds=Savings")]
    [InlineData("start=2024-03-01&end=2024-03-06&currencies=0")]
    [InlineData("start=2024-03-01&end=2024-03-06&currencies=5&currencies=-1")]
    [InlineData("start=2024-03-01&end=2024-03-06&currencies=abç")]
    public async Task Page_InvalidQuery_ReturnsBadRequest(string query)
    {
        Authorize("user", _testUserId, UserRole.User);
        var response = await Client.GetAsync($"api/TransactionLog/ItemsPage/{_testUserId}?{query}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _serviceMock.Verify(x => x.GetItemsPage(It.IsAny<int>(), It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Page_WithMoreThan100Currencies_ReturnsBadRequest()
    {
        Authorize("user", _testUserId, UserRole.User);
        var ids = string.Join('&', Enumerable.Range(1, 101).Select(_ => "currencies=USD"));

        var response = await Client.GetAsync($"api/TransactionLog/ItemsPage/{_testUserId}?start=2024-03-01&end=2024-03-06&{ids}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _serviceMock.Verify(x => x.GetItemsPage(It.IsAny<int>(), It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Page_BindsRepeatedKindsAndCurrencies()
    {
        Authorize("user", _testUserId, UserRole.User);

        var response = await Client.GetAsync(
            $"api/TransactionLog/ItemsPage/{_testUserId}?start=2024-03-01&end=2024-03-06&kinds=Bond&kinds=Investment&currencies=usd&currencies=EUR&currencies=USD",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _serviceMock.Verify(s => s.GetItemsPage(_testUserId, It.Is<TransactionLogQuery>(q =>
            q.Kinds!.SequenceEqual(new[] { TransactionKind.Bond, TransactionKind.Investment }) &&
            q.Currencies!.SequenceEqual(new[] { "USD", "EUR" })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, 641, HttpStatusCode.Unauthorized)]
    [InlineData(true, 999, HttpStatusCode.Forbidden)]
    public async Task Page_RequiresAuthenticatedOwner(bool authenticated, int userId, HttpStatusCode status)
    {
        ClearAuthorization();
        if (authenticated)
            Authorize("user", _testUserId, UserRole.User);
        var response = await Client.GetAsync($"api/TransactionLog/ItemsPage/{userId}?start=2024-03-01&end=2024-03-06", TestContext.Current.CancellationToken);
        Assert.Equal(status, response.StatusCode);
        _serviceMock.Verify(x => x.GetItemsPage(It.IsAny<int>(), It.IsAny<TransactionLogQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}