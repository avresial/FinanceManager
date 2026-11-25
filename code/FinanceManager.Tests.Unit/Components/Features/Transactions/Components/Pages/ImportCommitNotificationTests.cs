using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.Import;
using FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.Import;
using FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Components.Pages;
using FinanceManager.Components.Shared.Layout;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Tests.Unit.Components.Shared.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Components.Pages;

/// <summary>
/// A cash or bond import commits entries that can reveal Assets or Liabilities, or the dashboard itself, so the wizards announce
/// the commit through the same event the entry forms use. Each test starts from an empty session (no flags set in the menu).
/// </summary>
[Trait("Category", "Unit")]
public class ImportCommitNotificationTests
{
    private const string _bondCsv = "Date,Amount,Bond\n2026-01-01 10:00:00,100,Treasury\n";

    [Fact]
    public async Task BondImport_FromAnEmptySession_ShowsAssetsInTheMenu()
    {
        var backend = new NavMenuTestBackend();
        await using var context = CreateContext(BondRoutes(imported: 1, conflicts: []));
        context.Services.AddSingleton(backend.CreateService());
        var menu = context.Render<NavMenu>(parameters => parameters.Add(x => x.DrawerIsOpen, true));
        menu.WaitForAssertion(() => Assert.Contains(backend.Requests, path => path.StartsWith("api/Assets/", StringComparison.Ordinal)));
        Assert.Empty(menu.FindAll("[data-testid='nav-assets']"));
        var published = Count(context);
        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "bond"));

        backend.HasAssets = true;
        await BeginBondImport(cut);

        menu.WaitForAssertion(() => Assert.Single(menu.FindAll("[data-testid='nav-assets']")));
        Assert.Equal(1, published());
    }

    [Fact]
    public async Task BondImport_ThatOnlyFindsDuplicates_PublishesNothing()
    {
        await using var context = CreateContext(BondRoutes(imported: 0, conflicts: []));
        var published = Count(context);
        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "bond"));

        await BeginBondImport(cut);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='import-back']")));
        Assert.Equal(0, published());
    }

    [Fact]
    public async Task BondImport_AcceptedConflicts_PublishWhenTheyAreCommitted()
    {
        var existing = new BondAccountEntry(null, 1, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), 0, 50, 1) { UserId = 5 };
        var incoming = new BondEntryImport(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), 100, 1, string.Empty);
        await using var context = CreateContext(BondRoutes(imported: 0, conflicts: [new BondImportConflict(incoming, existing, "Same day")]));
        var published = Count(context);
        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "bond"));
        await BeginBondImport(cut);
        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<BondEntryConflictResolver>()));
        Assert.Equal(0, published());

        var resolver = cut.FindComponent<BondEntryConflictResolver>();
        cut.FindAll("button").First(button => button.TextContent.Contains("Keep all imported")).Click();
        await cut.InvokeAsync(() => resolver.Instance.SubmitAsync());

        cut.WaitForAssertion(() => Assert.Equal(1, published()));
    }

    [Fact]
    public async Task CashImport_FromAnEmptySession_PublishesOncePerFinishedJobWithImportedEntries()
    {
        await using var context = CreateContext(BondRoutes(imported: 0, conflicts: []));
        var published = Count(context);
        var tracker = context.Services.GetRequiredService<CurrencyImportJobTracker>();
        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "cash"));
        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<ImportCurrencyEntriesComponent>()));
        var jobId = Guid.NewGuid();
        tracker.JobId = jobId;

        await cut.InvokeAsync(() => tracker.Receive(Status(jobId, AsyncImportJobState.Running, processed: 1, imported: 1, completed: false)));
        Assert.Equal(0, published());

        await cut.InvokeAsync(() => tracker.Receive(Status(jobId, AsyncImportJobState.Completed, processed: 3, imported: 3, completed: true)));
        cut.WaitForAssertion(() => Assert.Equal(1, published()));
        await cut.InvokeAsync(() => tracker.Receive(Status(jobId, AsyncImportJobState.Completed, processed: 3, imported: 3, completed: true)));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(1, published());
    }

    [Fact]
    public async Task CashImport_ThatImportedNothing_PublishesNothing()
    {
        await using var context = CreateContext(BondRoutes(imported: 0, conflicts: []));
        var published = Count(context);
        var tracker = context.Services.GetRequiredService<CurrencyImportJobTracker>();
        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "cash"));
        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<ImportCurrencyEntriesComponent>()));
        var jobId = Guid.NewGuid();
        tracker.JobId = jobId;

        await cut.InvokeAsync(() => tracker.Receive(Status(jobId, AsyncImportJobState.Failed, processed: 3, imported: 0, completed: true)));
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(0, published());
    }

    [Fact]
    public async Task CashImport_ResolvedConflicts_Publish()
    {
        await using var context = CreateContext(BondRoutes(imported: 0, conflicts: []));
        var published = Count(context);
        var tracker = context.Services.GetRequiredService<CurrencyImportJobTracker>();
        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "cash"));
        cut.WaitForAssertion(() => Assert.NotNull(cut.FindComponent<ImportCurrencyEntriesComponent>()));
        var jobId = Guid.NewGuid();
        tracker.JobId = jobId;
        var conflict = new ImportJobConflict("c1", new ImportConflict(null, null, "Same day"));
        await cut.InvokeAsync(() => tracker.Receive(Status(jobId, AsyncImportJobState.Running, processed: 1, imported: 0, completed: false, conflict)));

        // What the resolver reports to the wizard once the server has accepted the decisions.
        await cut.InvokeAsync(() => cut.FindComponent<ImportCurrencyEntriesComponent>().Instance.OnConflictsResolved(["c1"]));

        cut.WaitForAssertion(() => Assert.Equal(1, published()));
    }

    private static CurrencyImportJobStatusDto Status(Guid jobId, AsyncImportJobState state, int processed, int imported, bool completed, params ImportJobConflict[] conflicts) =>
        new(jobId, state, 3, processed, imported, 0, conflicts.Length, 0, [], conflicts, completed);

    private static Func<int> Count(BunitContext context)
    {
        var published = 0;
        context.Services.GetRequiredService<AccountDataSynchronizationService>().AccountsChanged += () => Interlocked.Increment(ref published);
        return () => Volatile.Read(ref published);
    }

    // Upload, map (suggested by the server), continue and begin the import.
    private static async Task BeginBondImport(IRenderedComponent<TransactionsImportPage> cut)
    {
        var file = new Mock<IBrowserFile>();
        file.SetupGet(x => x.Name).Returns("bonds.csv");
        file.SetupGet(x => x.Size).Returns(_bondCsv.Length);
        file.Setup(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(() => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_bondCsv)));
        await cut.InvokeAsync(() => cut.FindComponent<ImportCsvFileStep>().Instance.OnFilesChanged.InvokeAsync(new InputFileChangeEventArgs([file.Object])));
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button").Single(button => button.TextContent.Contains("Continue to mapping")).HasAttribute("disabled")));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Continue to mapping")).Click();
        cut.WaitForAssertion(() => Assert.False(cut.FindAll("button").Single(button => button.TextContent.Contains("Begin import")).HasAttribute("disabled")));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Begin import")).Click();
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> BondRoutes(int imported, IReadOnlyList<BondImportConflict> conflicts) => request =>
    {
        var path = request.RequestUri!.AbsolutePath;
        return path switch
        {
            "/api/Currency/GetAll" => Json(new List<Currency> { DefaultCurrency.PLN }),
            "/api/BondDetails" => Json(new List<BondDetails> { new("Treasury", "Issuer", new(2026, 1, 1), new(2036, 1, 1), []) { Id = 1 } }),
            "/api/CsvHeaderMapping/SuggestMappings" => Json(new List<HeaderMappingResultDto>
            {
                new("Date", "PostingDate"), new("Amount", "ValueChange"), new("Bond", "Bond"),
            }),
            "/api/transactions/bonds/import" => Json(new BondImportResult(imported, 0, [], conflicts)),
            "/api/transactions/bonds/import/resolve" => Json(new BondImportResult(1, 0, [], [])),
            _ => new HttpResponseMessage(HttpStatusCode.NoContent),
        };
    };

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private static BunitContext CreateContext(Func<HttpRequestMessage, HttpResponseMessage> routes)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession { UserId = 5, UserName = "user", Password = string.Empty, UserRole = UserRole.User });
        context.Services.AddSingleton(login.Object);
        var http = new HttpClient(new Routes(routes)) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(http);
        context.Services.AddSingleton(new CurrencyHttpClient(http));
        context.Services.AddSingleton(new BondDetailsHttpClient(http));
        context.Services.AddSingleton(new CashTransactionsHttpClient(http));
        context.Services.AddSingleton(new BondTransactionsHttpClient(http));
        context.Services.AddTransient<CsvHeaderMappingHttpClient>();
        context.Services.AddScoped<CurrencyImportJobTracker>();
        context.Services.AddSingleton<AccountDataSynchronizationService>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        return context;
    }

    private sealed class Routes(Func<HttpRequestMessage, HttpResponseMessage> routes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(routes(request));
    }
}