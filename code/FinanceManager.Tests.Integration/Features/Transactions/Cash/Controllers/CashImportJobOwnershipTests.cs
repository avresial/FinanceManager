using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Transactions.Cash.Controllers;

[Trait("Category", "Integration")]
public class CashImportJobOwnershipTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private readonly CurrencyImportJobStore _jobs = new();
    protected override bool ReuseApplicationHost => false;
    protected override void ConfigureServices(IServiceCollection services) => services.AddSingleton<ICurrencyImportJobStore>(_jobs);

    // Guest users have negative ids, so an owner check of `> 0` instead of `!= 0` would lock them out.
    [Theory]
    [InlineData(77, 78)]
    [InlineData(-77, -78)]
    public async Task ImportStatus_UsesTheAuthenticatedJobOwner(int owner, int other)
    {
        var job = _jobs.CreateOwnerJob(owner, 0, 1);
        var route = $"api/transactions/cash/import/jobs/{job}";
        Authorize("owner", owner, UserRole.User);
        var status = await Client.GetFromJsonAsync<CurrencyImportJobStatusDto>(route, TestContext.Current.CancellationToken);
        Assert.Equal(job, status!.JobId);

        Authorize("other", other, UserRole.User);
        using var foreign = await Client.GetAsync(route, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}