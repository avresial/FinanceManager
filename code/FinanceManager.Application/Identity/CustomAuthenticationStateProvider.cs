using FinanceManager.Domain.Identity.Entities;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace FinanceManager.Application.Identity;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private ClaimsPrincipal CurrentUser { get; set; } = GetAnonymous();

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(this.CurrentUser));

    public Task<AuthenticationState> ChangeUser(string username, string id, UserRole role)
    {
        this.CurrentUser = GetUser(username, id, role);
        var task = this.GetAuthenticationStateAsync();
        this.NotifyAuthenticationStateChanged(task);
        return task;
    }

    public Task<AuthenticationState> Logout()
    {
        this.CurrentUser = GetAnonymous();
        var task = this.GetAuthenticationStateAsync();
        this.NotifyAuthenticationStateChanged(task);
        return task;
    }

    private static ClaimsPrincipal GetUser(string userName, string id, UserRole role) =>
       new(new ClaimsIdentity(
       [
            new(ClaimTypes. Sid, id),
            new(ClaimTypes.Name, userName),
            .. role.GetAssignedRoles().Select(roleName => new Claim(ClaimTypes.Role, roleName))
       ], "Bearer"));


    private static ClaimsPrincipal GetAnonymous() =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Sid, "0"),
            new Claim(ClaimTypes.Name, "Anonymous"),
            new Claim(ClaimTypes.Role, "Anonymous")
        ], null));
}