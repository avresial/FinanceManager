using FinanceManager.Application.Options;
using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace FinanceManager.Application.Services;

public class PasswordResetService(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IOptions<PasswordResetOptions> options) : IPasswordResetService
{
    private const int _tokenByteLength = 32;
    private readonly PasswordResetOptions _options = options.Value;

    public async Task<string> RequestReset(string login, CancellationToken cancellationToken = default)
    {
        // Always mint a token so future email delivery can use the same caller path for registered and unknown
        // accounts. For an unknown login the token is returned to backend code but never persisted, so it can't
        // reset anything; controllers must not expose the raw token over HTTP.
        var rawToken = GenerateRawToken();

        if (string.IsNullOrWhiteSpace(login)) return rawToken;

        // Logins are stored lowercased at registration; normalise here so a request with different casing still
        // resolves the same account.
        var normalizedLogin = login.ToLowerInvariant();

        var user = await userRepository.GetUser(normalizedLogin);
        if (user is null) return rawToken;

        var now = DateTime.UtcNow;

        // Requesting a new link supersedes any earlier one, so only the freshest token can be redeemed.
        await tokenRepository.InvalidateActiveTokensForUser(user.UserId, now);

        await tokenRepository.Add(new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = Hash(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(_options.TokenValidityMinutes),
        });

        return rawToken;
    }

    public async Task<PasswordResetResult> ResetPassword(string rawToken, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(newPassword))
            return PasswordResetResult.Failure(PasswordResetStatus.InvalidToken);

        var existing = await tokenRepository.GetByHash(Hash(rawToken));
        if (existing is null)
            return PasswordResetResult.Failure(PasswordResetStatus.InvalidToken);

        if (existing.UsedAt is not null)
            return PasswordResetResult.Failure(PasswordResetStatus.AlreadyUsed);

        if (DateTime.UtcNow >= existing.ExpiresAt)
            return PasswordResetResult.Failure(PasswordResetStatus.Expired);

        // Claim the token atomically *before* changing the password. The conditional consume succeeds for only the
        // single caller that flips UsedAt from null, so two concurrent redemptions of the same link can't both clear
        // the check above and reset the password twice — the loser is rejected here rather than overwriting the
        // winner's new password. (The checks above stay as a cheap fast-path for the common, uncontended case.)
        if (!await tokenRepository.TryConsume(existing.TokenHash, DateTime.UtcNow))
            return PasswordResetResult.Failure(PasswordResetStatus.AlreadyUsed);

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(newPassword);
        var updated = await userRepository.UpdatePassword(existing.UserId, encryptedPassword);
        if (!updated)
            return PasswordResetResult.Failure(PasswordResetStatus.InvalidToken);

        return PasswordResetResult.Ok();
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(_tokenByteLength);
        // URL-safe base64 so the value can be dropped straight into a reset-link query string without escaping.
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Hash(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}