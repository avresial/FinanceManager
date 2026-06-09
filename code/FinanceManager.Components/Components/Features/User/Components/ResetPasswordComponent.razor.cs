using FinanceManager.Components.HttpClients;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.User.Components;

public partial class ResetPasswordComponent
{
    private MudForm? _form;
    private bool _success;
    private string[] _errors = [];
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isProcessing;
    private bool _resetComplete;

    [Parameter] public string? Token { get; set; }

    [Inject] public required ILogger<ResetPasswordComponent> Logger { get; set; }
    [Inject] public required PasswordResetHttpClient PasswordResetHttpClient { get; set; }

    private async Task Reset()
    {
        if (_form is null || _isProcessing || string.IsNullOrWhiteSpace(Token)) return;

        await _form.Validate();
        if (!_form.IsValid) return;

        _isProcessing = true;
        try
        {
            var succeeded = await PasswordResetHttpClient.ResetPassword(Token, _newPassword);
            if (succeeded)
            {
                _resetComplete = true;
                return;
            }

            _errors = ["This password reset link is invalid or has expired. Please request a new one."];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error resetting password.");
            _errors = ["Something went wrong. Please try again."];
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private string? PasswordMatch(string arg)
    {
        if (_newPassword != arg)
            return "Passwords don't match";

        return null;
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await Reset();
    }
}