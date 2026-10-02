using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.GiftCodes;
using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Application.Identity.GiftCodes;

public class GiftCodeService(IGiftCodeRepository repository)
{
    public async Task<GeneratedGiftCode> Generate(GenerateGiftCode request, int adminId, CancellationToken ct = default)
    {
        if (request.PricingLevel is not (PricingLevel.Basic or PricingLevel.Premium))
            throw new ArgumentException("Select Basic or Premium.", nameof(request));
        if (request.Note?.Length > 200)
            throw new ArgumentException("The note must be at most 200 characters.", nameof(request));

        // 128 bits of entropy; the full bearer code is returned once and never persisted.
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var code = string.Join('-', Enumerable.Range(0, 8).Select(i => raw.Substring(i * 4, 4)));
        var details = await repository.Add(new GiftCode
        {
            CodeHash = Hash(raw),
            CodeSuffix = raw[^4..],
            PricingLevel = request.PricingLevel,
            Note = request.Note?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = adminId
        }, ct);
        return new(code, details);
    }

    public Task<IReadOnlyList<GiftCodeDto>> List(int offset, int count, CancellationToken ct = default) =>
        repository.List(offset, count, ct);

    public Task<bool> Revoke(long id, int adminId, CancellationToken ct = default) =>
        repository.Revoke(id, adminId, DateTime.UtcNow, ct);

    public Task<GiftCodeRedemptionResult> Redeem(string? code, int userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64)
            return Task.FromResult(GiftCodeRedemptionResult.Unavailable);
        var normalized = code.Trim().Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        if (normalized.Length != 32 || !normalized.All(Uri.IsHexDigit))
            return Task.FromResult(GiftCodeRedemptionResult.Unavailable);
        return repository.Redeem(Hash(normalized), userId, DateTime.UtcNow, ct);
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}