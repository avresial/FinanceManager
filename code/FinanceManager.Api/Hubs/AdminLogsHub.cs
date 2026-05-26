using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Hubs;

[Authorize(Roles = "Admin")]
public class AdminLogsHub : Hub
{
    public const string GroupName = "admin-logs";

    public Task Subscribe() => Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
}