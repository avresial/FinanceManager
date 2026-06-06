using FinanceManager.Application.Commands.User;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceManager.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Authentication")]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public class PasswordResetController(
    IPasswordResetService passwordResetService,
    ILogger<PasswordResetController> logger) : ControllerBase
{
    // Same wording regardless of whether the account exists, so the response carries no account-enumeration signal.
    private const string _genericMessage =
        "If an account exists for that email, a password reset link has been sent.";

    /// <summary>
    /// Issues a single-use, time-limited reset token for the given login. Always returns 200 with an identical
    /// message so the response cannot be used to probe which emails are registered.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ForgotPasswordResponse))]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var rawToken = await passwordResetService.RequestReset(request.Login, cancellationToken);

        // TEMPORARY (issue #280): no email provider is wired up yet, so the raw token is returned in the response
        // and the client renders a direct "reset password" link from it. A token is returned for *every* request —
        // a real persisted one for registered accounts, a throwaway one otherwise — so neither the response nor the
        // link reveals whether the email is registered (#342). Once transactional email is in place the token must
        // stop being returned here and be delivered only via the emailed link.
        return Ok(new ForgotPasswordResponse(_genericMessage, rawToken));
    }

    /// <summary>Validates and consumes a reset token, setting the account's new password on success.</summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var result = await passwordResetService.ResetPassword(request.Token, request.NewPassword, cancellationToken);
        if (result.Succeeded) return Ok();

        logger.LogInformation("Password reset rejected with status {Status}.", result.Status);
        return BadRequest("This password reset link is invalid or has expired. Please request a new one.");
    }
}