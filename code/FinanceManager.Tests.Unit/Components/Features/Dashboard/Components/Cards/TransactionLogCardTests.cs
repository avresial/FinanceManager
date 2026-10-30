using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public sealed class TransactionLogCardTests
{
    [Fact]
    public async Task Header_OffersViewAllLinkToTransactionsPage()
    {
        await using var context = CreateContext([]);

        var cut = context.Render<TransactionLogCard>();

        cut.WaitForAssertion(() =>
        {
            var link = cut.Find("a[href='/Transactions']");
            Assert.Equal("View all", link.TextContent.Trim());
            Assert.Equal("View all transactions", link.GetAttribute("aria-label"));
        });
    }

    [Fact]
    public async Task Rows_LinkToOwningAccount()
    {
        var date = new DateTime(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc);
        await using var context = CreateContext(
        [
            new TransactionLogEntryDto(11, "Everyday", AccountType.Currency, 5, date, -12.5m, "Coffee"),
            new TransactionLogEntryDto(12, "Brokerage", AccountType.Stock, 6, date, 100m, ""),
        ]);

        var cut = context.Render<TransactionLogCard>();

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("a.fm-txlog-link");
            Assert.Equal(["/AccountDetails/11?entryId=5", "/AccountDetails/12"], rows.Select(row => row.GetAttribute("href")));
            Assert.Contains("Coffee", rows[0].TextContent);
            Assert.Contains("15 Oct 2026", rows[0].TextContent);
            // A blank description falls back to the account name.
            Assert.Contains("Brokerage", rows[1].QuerySelector(".fm-name")!.TextContent);
        });
    }

    private static BunitContext CreateContext(List<TransactionLogEntryDto> entries)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 7,
            UserName = "guest",
            Password = string.Empty,
            UserRole = UserRole.User
        });
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(new DashboardCardsSnapshotStore(new SnapshotRefreshCoordinator(
            Mock.Of<ISnapshotService>(), NullLogger<SnapshotRefreshCoordinator>.Instance)));
        context.Services.AddSingleton(new TransactionLogHttpClient(
            new HttpClient(new ListHandler(entries)) { BaseAddress = new Uri("http://localhost/") }));
        return context;
    }

    private sealed class ListHandler(List<TransactionLogEntryDto> entries) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(entries) });
    }
}