using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.FinancialAccounts.Currencies.Controllers;

[Trait("Category", "Integration")]
public class CurrencyAccountImportControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private readonly CurrencyImportJobStore _jobs = new();
    protected override bool ReuseApplicationHost => false;
    protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton<ICurrencyImportJobStore>(_jobs);

    [Fact]
    public async Task ImportStatus_UsesTheAuthenticatedJobOwner()
    {
        var job = _jobs.CreateOwnerJob(77, 0, 1);
        var route = $"api/transactions/cash/import/jobs/{job}";
        Authorize("owner", 77, UserRole.User);
        var status = await Client.GetFromJsonAsync<CurrencyImportJobStatusDto>(route, TestContext.Current.CancellationToken);
        Assert.Equal(job, status!.JobId);

        Authorize("other", 78, UserRole.User);
        using var foreign = await Client.GetAsync(route, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}