using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using System.Collections.Concurrent;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

/// <summary>
/// Coalesces concurrent requests for the same bond definition while keeping successful results for
/// the lifetime of the account-details component.
/// </summary>
public sealed class BondDetailsRequestLoader(Func<int, Task<BondDetails?>> fetch)
{
    private readonly ConcurrentDictionary<int, Lazy<Task<BondDetails?>>> _requests = [];

    public async Task<BondDetails?> LoadAsync(int bondDetailsId)
    {
        var request = _requests.GetOrAdd(
            bondDetailsId,
            id => new Lazy<Task<BondDetails?>>(
                () => fetch(id),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await request.Value;
        }
        catch
        {
            if (_requests.TryGetValue(bondDetailsId, out var current)
                && ReferenceEquals(current, request))
            {
                _requests.TryRemove(bondDetailsId, out _);
            }

            throw;
        }
    }
}