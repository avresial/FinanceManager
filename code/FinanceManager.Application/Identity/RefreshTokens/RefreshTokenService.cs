using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Shared.Services;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace FinanceManager.Application.Identity.RefreshTokens;

public class RefreshTokenService(IRefreshTokenRepository repository, IOptions<RefreshTokenOptions> options, IDateTimeProvider dateTimeProvider) : IRefreshTokenService
{
    private const int _tokenByteLength = 32;
    private readonly RefreshTokenOptions _options = options.Value;

    public async Task<string> Issue(int userId, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateRawToken();
        var now = dateTimeProvider.UtcNow;

        await repository.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            FamilyId = Guid.NewGuid(),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_options.SlidingValidityDays),
            AbsoluteExpiresAt = now.AddDays(_options.AbsoluteValidityDays),
        });

        return rawToken;
    }

    public async Task<RefreshTokenRotationResult> ValidateAndRotate(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return RefreshTokenRotationResult.Failure(RefreshTokenStatus.NotFound);

        var existing = await repository.GetByHash(Hash(rawToken));
        if (existing is null)
            return RefreshTokenRotationResult.Failure(RefreshTokenStatus.NotFound);

        var now = dateTimeProvider.UtcNow;

        // A token is only ever revoked once it has been rotated (or the family was nuked). Seeing it again means
        // someone is replaying a stolen, already-used token — revoke the whole family and force a fresh login.
        if (existing.RevokedAt is not null)
        {
            await repository.RevokeFamily(existing.FamilyId, now);
            return RefreshTokenRotationResult.Failure(RefreshTokenStatus.Revoked);
        }

        if (now >= existing.ExpiresAt || now >= existing.AbsoluteExpiresAt)
            return RefreshTokenRotationResult.Failure(RefreshTokenStatus.Expired);

        var newRawToken = GenerateRawToken();
        var replacement = new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = Hash(newRawToken),
            FamilyId = existing.FamilyId,
            CreatedAt = now,
            ExpiresAt = Min(now.AddDays(_options.SlidingValidityDays), existing.AbsoluteExpiresAt),
            AbsoluteExpiresAt = existing.AbsoluteExpiresAt,
        };

        existing.RevokedAt = now;
        existing.ReplacedByTokenHash = replacement.TokenHash;

        // Revoke-and-replace in one commit so a failure can't strand the old token revoked without a successor.
        await repository.Rotate(existing, replacement);

        return RefreshTokenRotationResult.Ok(existing.UserId, newRawToken);
    }

    public async Task Revoke(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;

        var existing = await repository.GetByHash(Hash(rawToken));
        if (existing is null || existing.RevokedAt is not null) return;

        existing.RevokedAt = dateTimeProvider.UtcNow;
        await repository.Update(existing);
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(_tokenByteLength);
        // URL-safe base64 so the value is safe to drop straight into a cookie without further encoding.
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Hash(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static DateTime Min(DateTime left, DateTime right) => left < right ? left : right;
}