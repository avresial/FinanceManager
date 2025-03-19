using FinanceManager.Components.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components
{
    public partial class LoginComponent
    {
        private const string guestLogin = "Guest";
        private bool success;
        private string[] errors = { };
        private MudForm? form;
        private LoginModel loginModel = new();

        [Inject]
        public required ILogger<LoginComponent> Logger { get; set; }

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

        private async Task ValidFormSubmitted(EditContext editContext)
        {
            if (loginModel.Login is null || loginModel.Password is null) return;
            if (loginModel.Login == guestLogin)
            {
                await LogGuest();
            }
            else
            {
                await LoginService.Login(loginModel.Login, loginModel.Password);
                Navigation.NavigateTo("");
            }
        }

        private async Task Login()
        {
            if (loginModel.Login is null || loginModel.Password is null) return;
            if (form is null) return;
            await form.Validate();

            var loginResult = await LoginService.Login(loginModel.Login, loginModel.Password);

            if (loginResult) Navigation.NavigateTo("");

            errors = ["Incorrect username or password."];
            loginModel.Password = string.Empty;
        }

        private void InvalidFormSubmitted(EditContext editContext)
        {
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