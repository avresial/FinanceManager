using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.User.Components;

public partial class ForgotPasswordComponent
{
    private MudForm? _form;
    private LoginModel _loginModel = new();
    private string[] _errors = [];
    private bool _isProcessing;
    private bool _requestSent;
    private string _message = string.Empty;
    private string? _resetLink;

    [Inject] public required ILogger<ForgotPasswordComponent> Logger { get; set; }
    [Inject] public required PasswordResetHttpClient PasswordResetHttpClient { get; set; }

    private async Task RequestReset()
    {
        if (_form is null || _isProcessing) return;

        await _form.Validate();
        if (!_form.IsValid || string.IsNullOrWhiteSpace(_loginModel.Login)) return;

        _isProcessing = true;
        try
        {
            var response = await PasswordResetHttpClient.ForgotPassword(_loginModel.Login);

            // The API always answers with the same enumeration-safe message; treat a null (transport failure) as a
            // generic error rather than leaking anything about the account.
            if (response is null)
            {
                _errors = ["Something went wrong. Please try again."];
                return;
            }

            _message = response.Message;

            // TEMPORARY (issue #280): with no email provider, the API hands back the raw token so we can build a
            // direct reset link here. Once email delivery lands this returns null and the link disappears.
            if (!string.IsNullOrEmpty(response.ResetToken))
                _resetLink = $"reset-password?token={Uri.EscapeDataString(response.ResetToken)}";

            _requestSent = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error requesting password reset.");
            _errors = ["Something went wrong. Please try again."];
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await RequestReset();
    }
}