using FinanceManager.Domain.Commands.User;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class PasswordResetHttpClient(HttpClient httpClient)
{
    /// <summary>
    /// Requests a password reset link for the given login. Returns the server's response, whose message is identical
    /// whether or not the account exists. While no email provider is wired up the response also carries the raw reset
    /// token so the UI can show a direct link (issue #280).
    /// </summary>
    public async Task<ForgotPasswordResponse?> ForgotPassword(string login)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{httpClient.BaseAddress}api/PasswordReset/forgot-password", new ForgotPasswordRequest(login));

        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
    }

    /// <summary>Submits a new password for the given reset token. Returns <c>true</c> when the reset succeeded.</summary>
    public async Task<bool> ResetPassword(string token, string newPassword)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{httpClient.BaseAddress}api/PasswordReset/reset-password", new ResetPasswordRequest(token, newPassword));

        return response.IsSuccessStatusCode;
    }
}