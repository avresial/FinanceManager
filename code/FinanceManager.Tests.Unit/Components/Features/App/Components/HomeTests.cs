using Bunit;
using Bunit.TestDoubles;
using FinanceManager.Components.Features.App.Components;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;
using DashboardComponent = FinanceManager.Components.Features.Dashboard.Components.Dashboard;

namespace FinanceManager.Tests.Unit.Components.Features.App.Components;

[Trait("Category", "Unit")]
public class HomeTests
{
    [Fact]
    public async Task GuestWithTransactionsButNoAccounts_OpensTheDashboard()
    {
        await using var context = CreateContext(accountCount: 0, transactionCount: 1, userId: -5, out var accounts, out var transactions);

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<Stub<DashboardComponent>>()));
        Assert.Empty(cut.FindComponents<Stub<WelcomePage>>());
        accounts.Verify(service => service.GetAvailableAccounts(), Times.Once);
        Assert.Equal(1, transactions.Requests);
        Assert.Equal("api/TransactionLog/Get/-5?count=1", transactions.LastRequestPath);
    }

    [Fact]
    public async Task UserWithNoTransactionsAndNoAccounts_OpensTheWelcomePage()
    {
        await using var context = CreateContext(accountCount: 0, transactionCount: 0, userId: 3, out _, out _);

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<Stub<WelcomePage>>()));
        Assert.Empty(cut.FindComponents<Stub<DashboardComponent>>());
    }

    [Fact]
    public async Task UserWithAnAccountButNoTransactions_OpensTheDashboard_WithoutAskingTheTransactionLog()
    {
        await using var context = CreateContext(accountCount: 1, transactionCount: 0, userId: 3, out _, out var transactions);

        var cut = context.Render<Home>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<Stub<DashboardComponent>>()));
        Assert.Equal(0, transactions.Requests);
    }

    private static BunitContext CreateContext(int accountCount, int transactionCount, int userId, out Mock<IFinancialAccountService> accounts, out TransactionLogHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.ComponentFactories.AddStub<DashboardComponent>();
        context.ComponentFactories.AddStub<WelcomePage>();

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = userId, UserName = userId < 0 ? "guest" : "user", Password = string.Empty, UserRole = UserRole.User, Token = "t" });
        context.Services.AddSingleton(login.Object);

        accounts = new Mock<IFinancialAccountService>();
        accounts.Setup(service => service.GetAvailableAccounts()).ReturnsAsync(Enumerable.Range(1, accountCount).ToDictionary(id => id, _ => typeof(object)));
        context.Services.AddSingleton(accounts.Object);

        handler = new TransactionLogHandler(transactionCount);
        context.Services.AddSingleton(new TransactionLogHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
        context.Services.AddLogging();
        return context;
    }

    private static TransactionLogEntryDto Entry(int id) => new(TransactionKind.Cash, id + 1, new DateTime(2026, 1, 1), 10m, "Entry");

    private sealed class TransactionLogHandler(int transactionCount) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? LastRequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastRequestPath = request.RequestUri!.PathAndQuery.TrimStart('/');
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Enumerable.Range(0, transactionCount).Select(id => Entry(id)).ToList())
            });
        }
    }
}