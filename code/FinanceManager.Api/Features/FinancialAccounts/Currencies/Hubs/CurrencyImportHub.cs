using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using FinanceManager.Api.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Hubs;

[Authorize]
public class CurrencyImportHub(ICurrencyImportJobStore jobStore) : Hub
{
    public static string GetJobGroupName(Guid jobId) => $"currency-import-{jobId:N}";

    public async Task JoinJob(string jobId)
    {
        if (!Guid.TryParse(jobId, out var parsedJobId))
            throw new HubException("Invalid job id.");

        if (Context.User is null)
            throw new HubException("Authentication is required.");

        var userId = ApiAuthenticationHelper.GetUserId(Context.User);
        if (!jobStore.CanAccessJob(parsedJobId, userId))
            throw new HubException("Job not found or access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GetJobGroupName(parsedJobId));
    }
}