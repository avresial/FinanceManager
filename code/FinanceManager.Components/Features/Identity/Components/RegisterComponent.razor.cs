using Blazored.LocalStorage;
using FinanceManager.Components.Features.Identity.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Components.Features.Identity.Components;

public partial class RegisterComponent
{

    private bool _success;
    private string[] _errors = [];
    private MudForm? _form;
    private LoginModel _loginModel = new();

    public string? ConfirmPassword { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

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
            if (!await UserService.AddUser(_loginModel.Login, _loginModel.Password, PricingLevel, FirstName, LastName))
                newErrors.Add("Incorrect username or password.");
            else if ((await LoginService.Login(_loginModel.Login, _loginModel.Password)).IsSuccess)
                Navigation.NavigateTo("");
        }

        _errors = newErrors.ToArray();
        _loginModel.Password = string.Empty;
    }
    private static string? ValidateEmail(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return "Email is required";

        return new EmailAddressAttribute().IsValid(arg) ? null : "Invalid email address";
    }
    private string? PasswordMatch(string arg)
    {
        if (_loginModel.Password != arg)
            return "Passwords don't match";

        return null;
    }
}