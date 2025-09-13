using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace FinanceManager.Application.Providers;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    public CustomAuthenticationStateProvider()
    {
        this.CurrentUser = this.GetAnonymous();
    }

    private ClaimsPrincipal CurrentUser { get; set; }

    private ClaimsPrincipal GetUser(string userName, string id, string role) =>
        new(new ClaimsIdentity(
        [
            new(ClaimTypes. Sid, id),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, role)
        ], "Bearer"));


    private ClaimsPrincipal GetAnonymous() =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Sid, "0"),
            new Claim(ClaimTypes.Name, "Anonymous"),
            new Claim(ClaimTypes.Role, "Anonymous")
        ], null));


    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(this.CurrentUser));

    public Task<AuthenticationState> ChangeUser(string username, string id, string role)
    {
        this.CurrentUser = this.GetUser(username, id, role);
        var task = this.GetAuthenticationStateAsync();
        this.NotifyAuthenticationStateChanged(task);
        return task;
    }

    public Task<AuthenticationState> Logout()
    {
        this.CurrentUser = this.GetAnonymous();
        var task = this.GetAuthenticationStateAsync();
        this.NotifyAuthenticationStateChanged(task);
        return task;
    }
}
