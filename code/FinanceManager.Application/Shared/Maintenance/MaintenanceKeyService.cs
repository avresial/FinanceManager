using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Shared.Maintenance.Entities;
using FinanceManager.Domain.Shared.Maintenance.Repositories;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Application.Shared.Maintenance;

public class MaintenanceKeyService(
    IMaintenanceKeyRepository repository,
    IOptions<MaintenanceOptions> options) : IMaintenanceKeyService
{
    // Recognisable prefix so a leaked key is identifiable in logs/secret scanners without
    // revealing what it opens; 32 random bytes give 256 bits of entropy.
    private const string _keyPrefix = "fmk_";

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var plaintext = _keyPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        await repository.Save(new MaintenanceApiKey
        {
            KeyHash = Hash(plaintext),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        return plaintext;
    }

    public Task<bool> RevokeAsync(CancellationToken ct = default) => repository.Delete(ct);

    public async Task<MaintenanceKeyStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var key = await repository.Get(ct);
        return new MaintenanceKeyStatus(key is not null, key?.CreatedAt);
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.ApiKey)) return true;
        return await repository.Get(ct) is not null;
    }

    public async Task<bool> ValidateAsync(string providedKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(providedKey)) return false;

        var storedKey = await repository.Get(ct);
        if (storedKey is not null && FixedTimeEquals(Hash(providedKey), storedKey.KeyHash))
            return true;

        // Config-supplied key stays valid as a break-glass path (e.g. the database is unreachable
        // for key management but an operator can still set an environment variable).
        var configuredKey = options.Value.ApiKey;
        return !string.IsNullOrWhiteSpace(configuredKey) && FixedTimeEquals(providedKey, configuredKey);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    // Constant-time comparison so response timing cannot be used to guess the key byte by byte.
    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}