using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Commands;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceManager.Api.Features.Identity.Controllers;

[Route("api/[controller]")]
[ApiController]
[Tags("Authentication")]
[EnableRateLimiting(RateLimitingServiceCollectionExtension.AuthPolicy)]
public class PasswordResetController(
    IPasswordResetService passwordResetService,
    IWebHostEnvironment environment,
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
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (UnavailableOutsideDevelopment() is { } unavailable) return unavailable;

        await passwordResetService.RequestReset(request.Login, cancellationToken);

        // The raw token must never be returned over HTTP; issue #280 will add email-only delivery.
        return Ok(new ForgotPasswordResponse(_genericMessage));
    }

    /// <summary>Validates and consumes a reset token, setting the account's new password on success.</summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (UnavailableOutsideDevelopment() is { } unavailable) return unavailable;

        var result = await passwordResetService.ResetPassword(request.Token, request.NewPassword, cancellationToken);
        if (result.Succeeded) return Ok();

        logger.LogInformation("Password reset rejected with status {Status}.", result.Status);
        return BadRequest("This password reset link is invalid or has expired. Please request a new one.");
    }

    private IActionResult? UnavailableOutsideDevelopment()
    {
        if (environment.IsDevelopment()) return null;

        var pathForLog = HttpContext.Request.Path.ToString()
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        logger.LogWarning(
            "Password reset endpoint {Path} rejected because reset token delivery is not configured outside Development.",
            pathForLog);
        return StatusCode(StatusCodes.Status501NotImplemented, "Password reset is not available.");
    }
}