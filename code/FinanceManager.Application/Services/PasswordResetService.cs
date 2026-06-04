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

    public async Task<string?> RequestReset(string login, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login)) return null;

        // Logins are stored lowercased at registration; normalise here so a request with different casing still
        // resolves the same account.
        var normalizedLogin = login.ToLowerInvariant();

        var user = await userRepository.GetUser(normalizedLogin);
        if (user is null) return null;

        var now = DateTime.UtcNow;

        // Requesting a new link supersedes any earlier one, so only the freshest token can be redeemed.
        await tokenRepository.InvalidateActiveTokensForUser(user.UserId, now);

        var rawToken = GenerateRawToken();
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

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(newPassword);
        var updated = await userRepository.UpdatePassword(existing.UserId, encryptedPassword);
        if (!updated)
            return PasswordResetResult.Failure(PasswordResetStatus.InvalidToken);

        // Consume the token only after the password change has been persisted, so a failed update can be retried
        // with the same link rather than silently burning it.
        existing.UsedAt = DateTime.UtcNow;
        await tokenRepository.Update(existing);

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