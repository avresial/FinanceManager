using Blazored.LocalStorage;
using FinanceManager.Components.ViewModels;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components;

public partial class RegisterComponent
{

    private bool _success;
    private string[] _errors = [];
    private MudForm? _form;
    private LoginModel _loginModel = new();

    public string? ConfirmPassword { get; set; }

    [Inject] public required NavigationManager Navigation { get; set; }
    [Inject] public required IUserService UserService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILocalStorageService LocalStorageService { get; set; }

    [Parameter] public PricingLevel PricingLevel { get; set; }

    private async Task Register()
    {
        if (_form is not null)
        {
            await _form.Validate();
            if (!_form.IsValid || _loginModel is null || string.IsNullOrEmpty(_loginModel.Login) || string.IsNullOrEmpty(_loginModel.Password))
            {
                _errors = ["Incorrect username or password"];
                return;
            }
        }
        List<string> newErrors = [];
        if (_loginModel.Login is not null && _loginModel.Password is not null)
        {
            var addingUserResult = await UserService.AddUser(_loginModel.Login, _loginModel.Password, PricingLevel);

            if (!addingUserResult)
                newErrors.Add("Incorrect username or password.");

            var loginResult = await LoginService.Login(_loginModel.Login, _loginModel.Password);

            if (loginResult)
                Navigation.NavigateTo("");
        }

        _errors = newErrors.ToArray();
        _loginModel.Password = string.Empty;
    }
    private string? PasswordMatch(string arg)
    {
        if (_loginModel.Password != arg)
            return "Passwords don't match";

        return null;
    }
}