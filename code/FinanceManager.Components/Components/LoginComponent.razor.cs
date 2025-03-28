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
        private const string guestLogin = "Guest";
        private bool success;
        private string[] errors = [];
        private MudForm? form;
        private LoginModel loginModel = new();

        [Inject]
        public required ILogger<LoginComponent> Logger { get; set; }

        [Inject]
        public required NavigationManager Navigation { get; set; }

        [Inject]
        public required ILoginService LoginService { get; set; }

        [Inject]
        public required ILocalStorageService LocalStorageService { get; set; }

        [Inject]
        public required IFinancialAccountService FinancalAccountService { get; set; }

        protected override async Task OnInitializedAsync()
        {
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

            var getKeepMeLoggedinSession = await LoginService.GetKeepMeLoggedinSession();
            if (getKeepMeLoggedinSession is not null)
            {
                var result = await LoginService.Login(getKeepMeLoggedinSession);
                if (result)
                    Navigation.NavigateTo("");
            }
        }

        private async Task Login()
        {
            if (loginModel.Login is null || loginModel.Password is null) return;
            if (form is null) return;
            await form.Validate();

            var loginResult = await LoginService.Login(loginModel.Login, loginModel.Password);

            if (loginResult)
            {
                Navigation.NavigateTo("");
                return;
            }

            errors = ["Incorrect username or password."];
            loginModel.Password = string.Empty;
        }

        private async Task LogGuest()
        {
            try
            {
                FinancalAccountService.InitializeMock();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

            await LoginService.Login(guestLogin, "GuestPassword");
            Navigation.NavigateTo("");
        }
    }
}