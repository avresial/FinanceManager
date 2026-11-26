using FinanceManager.Api.Features.Transactions.Cash.Services;
using FinanceManager.Api.Shared.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Features.Transactions.Cash.Hubs;

[Authorize]
public class CashImportHub(ICashImportJobStore jobStore) : Hub
{
    public static string GetJobGroupName(Guid jobId) => $"currency-import-{jobId:N}";

    public async Task JoinJob(string jobId)
    {
        if (!Guid.TryParse(jobId, out var parsedJobId))
            throw new HubException("Invalid job id.");

        if (Context.User is null)
            throw new HubException("Authentication is required.");

        if (!ApiAuthenticationHelper.TryGetOwnerId(Context.User, out var userId) || !jobStore.CanAccessJob(parsedJobId, userId))
            throw new HubException("Job not found or access denied.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GetJobGroupName(parsedJobId));
    }
}