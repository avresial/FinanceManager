using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Features.Administration.Hubs;

[Authorize(Roles = "Admin")]
public class AdminLogsHub : Hub
{
    public const string GroupName = "admin-logs";

    public Task Subscribe() => Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
}