using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FinanceManager.Api.Features.Labels.Hubs;

[Authorize(Roles = "Admin")]
public class LabelSetterProgressHub : Hub
{
    public const string GroupName = "label-setter-progress";

    public Task Subscribe() => Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
}