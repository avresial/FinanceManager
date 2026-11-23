using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.Import;
using FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.Import;
using FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Components.Pages;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Components.Pages;

[Trait("Category", "Unit")]
public class TransactionsImportPageTests
{
    [Theory]
    [InlineData("cash")]
    [InlineData("Cash")]
    public async Task Cash_RendersTheCashWizardWithoutAnAccount(string kind)
    {
        await using var context = CreateContext();

        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, kind));

        var wizard = cut.FindComponent<ImportCurrencyEntriesComponent>();
        Assert.Null(wizard.Instance.AccountId);
        Assert.Empty(cut.FindComponents<ImportBondEntriesComponent>());
    }

    [Fact]
    public async Task Bond_RendersTheBondWizardWithoutAnAccount()
    {
        await using var context = CreateContext();

        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "bond"));

        Assert.Null(cut.FindComponent<ImportBondEntriesComponent>().Instance.AccountId);
        Assert.Empty(cut.FindComponents<ImportCurrencyEntriesComponent>());
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("bond")]
    public async Task EveryStep_OffersAWayBackToTransactions(string kind)
    {
        await using var context = CreateContext();

        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, kind));

        Assert.Equal("/Transactions", cut.Find("[data-testid=import-back]").GetAttribute("href"));
    }

    [Fact]
    public async Task AnUnknownKind_ShowsAnInfoAlertLinkingBack()
    {
        await using var context = CreateContext();

        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "investment"));

        Assert.Empty(cut.FindComponents<ImportCurrencyEntriesComponent>());
        Assert.Equal("/Transactions", cut.Find(".mud-alert a").GetAttribute("href"));
    }

    [Fact]
    public async Task CashWizard_AsksForTheCurrencyAndKeepsContinueOffUntilAFileIsLoaded()
    {
        await using var context = CreateContext();

        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "cash"));

        cut.WaitForAssertion(() => Assert.Contains("PLN cash", cut.FindComponent<ImportCurrencyEntriesComponent>().Instance.AccountName));
        Assert.NotEmpty(cut.FindAll("[data-testid=import-currency]"));
        Assert.True(cut.FindAll("button").Single(button => button.TextContent.Contains("Continue to mapping")).HasAttribute("disabled"));
    }

    [Fact]
    public async Task CashWizard_WithoutACurrency_DoesNotCompleteStep1WhenAFileIsLoaded()
    {
        // No currencies load, so there is nothing to import into; a parsed file must not unlock the later steps.
        await using var context = CreateContext(new RecordingHttpHandler(request => request.RequestUri!.AbsolutePath == "/api/Currency/GetAll"
            ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(new List<Currency>()) }
            : new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(Array.Empty<int>()) }));
        var cut = context.Render<TransactionsImportPage>(p => p.Add(x => x.Kind, "cash"));
        var file = new Mock<IBrowserFile>();
        file.SetupGet(x => x.Name).Returns("statement.csv");
        file.SetupGet(x => x.Size).Returns(40);
        file.Setup(x => x.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(() => new MemoryStream("Date;Amount\n2026-01-01;5\n2026-01-02;6\n"u8.ToArray()));

        await cut.InvokeAsync(() => cut.FindComponent<ImportCsvFileStep>().Instance.OnFilesChanged.InvokeAsync(new InputFileChangeEventArgs([file.Object])));

        cut.WaitForAssertion(() => Assert.Contains("Select a currency before continuing.", cut.Markup));
    }

    [Fact]
    public async Task CashWizardOfAnAccount_HasNoCurrencyChoice()
    {
        await using var context = CreateContext();

        var cut = context.Render<ImportCurrencyEntriesComponent>(p => p.Add(x => x.AccountId, 1));

        Assert.Empty(cut.FindAll("[data-testid=import-currency]"));
    }

    private static BunitContext CreateContext() => CreateContext(RecordingHttpHandler.ForEntryForms());

    private static BunitContext CreateContext(RecordingHttpHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var http = handler.CreateClient();
        context.Services.AddSingleton(Mock.Of<ILoginService>());
        context.Services.AddSingleton(Mock.Of<IFinancialAccountService>());
        context.Services.AddSingleton(http);
        context.Services.AddSingleton(new CurrencyHttpClient(http));
        context.Services.AddSingleton(new BondDetailsHttpClient(http));
        context.Services.AddSingleton(new CashTransactionsHttpClient(http));
        context.Services.AddSingleton(new BondTransactionsHttpClient(http));
        context.Services.AddTransient<CurrencyAccountImportHttpClient>();
        context.Services.AddTransient<CurrencyAccountHttpClient>();
        context.Services.AddTransient<BondAccountImportHttpClient>();
        context.Services.AddTransient<CsvHeaderMappingHttpClient>();
        context.Services.AddScoped<CurrencyImportJobTracker>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrency()).Returns(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);
        return context;
    }
}