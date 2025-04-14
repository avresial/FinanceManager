using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace FinanceManager.WebUi.Layout;

public partial class LoginDisplay
{
    private UserSession? _userSession = null;
    private bool _open;
    private string _label = "";
    private PricingLevel _pricingLabel;

    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required IUserService UserService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _userSession = await LoginService.GetLoggedUser();
        _label = _userSession?.UserName ?? "Login";
        if (_userSession is null) return;

        var user = await UserService.GetUser(_userSession?.UserId ?? 0);
        if (user is not null)
            _pricingLabel = user.PricingLevel;
    }

    public async Task BeginLogOut()
    {
        await LoginService.Logout();
        Navigation.NavigateToLogout("login");
    }
}