using Bunit;
using FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Components.Shared.Import;

[Trait("Category", "Unit")]
public class ImportStepCaptionTests
{
    private static readonly string[] _steps = ["Select file", "Map columns", "Review"];

    [Theory]
    [InlineData(0, "Step 1 of 3 · Map columns next")]
    [InlineData(1, "Step 2 of 3 · Review next")]
    [InlineData(2, "Step 3 of 3")]
    public void Text_NamesThePositionAndTheNextStep(int activeIndex, string expected) =>
        Assert.Equal(expected, ImportStepCaption.Text(activeIndex, _steps));

    [Fact]
    public void Text_IsEmptyWithoutSteps() => Assert.Equal(string.Empty, ImportStepCaption.Text(0, []));
    [Fact]
    public async Task CurrencyImport_AdvancingStepUpdatesCaption()
    {
        await using var context = new Bunit.BunitContext();
        context.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        context.Services.AddSingleton(Moq.Mock.Of<FinanceManager.Domain.Identity.Services.ILoginService>());
        context.Services.AddSingleton(Moq.Mock.Of<FinanceManager.Components.Features.FinancialAccounts.Services.IFinancialAccountService>());
        context.Services.AddSingleton(new HttpClient { BaseAddress = new Uri("http://localhost/") });
        context.Services.AddTransient<FinanceManager.Components.Features.FinancialAccounts.HttpClients.CurrencyAccountImportHttpClient>();
        context.Services.AddTransient<FinanceManager.Components.Features.FinancialAccounts.HttpClients.CurrencyAccountHttpClient>();
        context.Services.AddTransient<FinanceManager.Components.Features.FinancialAccounts.HttpClients.CsvHeaderMappingHttpClient>();
        context.Services.AddScoped<FinanceManager.Components.Features.FinancialAccounts.Services.CurrencyImportJobTracker>();

        var cut = context.Render<FinanceManager.Components.Features.FinancialAccounts.Components.CurrencyAccountComponents.Import.ImportCurrencyEntriesComponent>(p => p.Add(x => x.AccountId, 1));
        cut.WaitForAssertion(() => Assert.Contains("Step 1 of 3", cut.Find(".fm-import-step-caption").TextContent));
        var stepper = cut.FindComponent<MudBlazor.MudStepper>();
        await cut.InvokeAsync(() => stepper.Instance.ActiveIndexChanged.InvokeAsync(1));
        cut.WaitForAssertion(() => Assert.Contains("Step 2 of 3", cut.Find(".fm-import-step-caption").TextContent));
    }
}