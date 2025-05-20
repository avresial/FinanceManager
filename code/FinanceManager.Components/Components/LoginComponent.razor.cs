using Blazored.LocalStorage;
using FinanceManager.Components.Services;
using FinanceManager.Components.ViewModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components
{
    public partial class LoginComponent
    {
        private const string _guestLogin = "Guest";
        private bool _success;
        private string[] _errors = [];
        private MudForm? _form;
        private LoginModel _loginModel = new();

        [Inject] public required ILogger<LoginComponent> Logger { get; set; }
        [Inject] public required NavigationManager Navigation { get; set; }
        [Inject] public required ILoginService LoginService { get; set; }
        [Inject] public required ILocalStorageService LocalStorageService { get; set; }
        [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }

        protected override async Task OnInitializedAsync()
        {
#if DEBUG
            //await LoginService.Login("admin", "admin");
            //Navigation.NavigateTo("Admin/Dashboard");
            //return;
#endif

            bool firstVisit = !(await LocalStorageService.ContainKeyAsync("isThisFirstVisit"));
            if (firstVisit)
            {
                await LocalStorageService.SetItemAsync("isThisFirstVisit", false);
                Navigation.NavigateTo("landingpage");
                return;
            }

            var loggedUser = await LoginService.GetLoggedUser();
            if (loggedUser is not null)
                Navigation.NavigateTo("");

            var getKeepMeLoggedinSession = await LoginService.GetKeepMeLoggedInSession();
            if (getKeepMeLoggedinSession is not null)
            {
                var result = await LoginService.Login(getKeepMeLoggedinSession);
                if (result)
                    Navigation.NavigateTo("");
            }
        }

        private async Task Login()
        {
            if (_loginModel.Login is null || _loginModel.Password is null) return;
            if (_form is null) return;
            await _form.Validate();

            var loginResult = await LoginService.Login(_loginModel.Login, _loginModel.Password);

            if (loginResult)
            {
                var loggedUser = await LoginService.GetLoggedUser();
                if (loggedUser is null) return;

                switch (loggedUser.UserRole)
                {
                    case Domain.Enums.UserRole.User:
                        Navigation.NavigateTo("");
                        break;
                    case Domain.Enums.UserRole.Admin:
                        Navigation.NavigateTo("Admin/Dashboard");
                        break;
                    default:
                        Navigation.NavigateTo("");
                        break;
                }

                return;
            }

            _errors = ["Incorrect username or password."];
            _loginModel.Password = string.Empty;
        }

        private async Task LogGuest()
        {
            try
            {
                FinancialAccountService.InitializeMock();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

            await LoginService.Login(_guestLogin, "GuestPassword");
            Navigation.NavigateTo("");
        }
    }
}