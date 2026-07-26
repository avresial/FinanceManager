using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Identity.Components;

/// <summary>
/// Develop-only auto test login entry path (see <c>AGENTS.md</c>): <c>/DevelopLogin/{login}</c> signs straight in
/// as <c>guest</c> or <c>testuser</c> and lands on the dashboard, or on <c>{TargetPage}</c> when one is given
/// (e.g. <c>/DevelopLogin/guest/Assets</c>). The API refuses the login in Production/Release, so outside
/// development-like environments this page only ever shows the failure message.
/// </summary>
public partial class DevelopLoginPage
{
    private string? _error;

    [Parameter] public string Login { get; set; } = string.Empty;
    [Parameter] public string? TargetPage { get; set; }

    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!await LoginService.DevelopLogin(Login))
        {
            _error = $"Develop login failed for '{Login}'. It only accepts 'guest' or 'testuser' and is disabled in Production/Release.";
            return;
        }

        Navigation.NavigateTo(GetRedirectTarget(TargetPage));
    }

    // Only in-app relative paths may ride along — an absolute URL would let a crafted link bounce a freshly
    // authenticated session to another origin.
    private static string GetRedirectTarget(string? page)
    {
        if (string.IsNullOrWhiteSpace(page)) return string.Empty;

        var target = page.TrimStart('/');
        return Uri.TryCreate(target, UriKind.Absolute, out _) ? string.Empty : target;
    }
}