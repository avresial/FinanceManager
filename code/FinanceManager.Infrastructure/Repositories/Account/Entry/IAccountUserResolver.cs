namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

/// <summary>
/// Resolves the owning user of an account. The cached entry-repository decorators work by
/// <c>accountId</c> but tag their cache entries per user (<c>acc:u{userId}</c>), so they need this lookup
/// to build the tag. Implementations are expected to cache the mapping — account ownership does not
/// change — so the resolution adds at most one DB round trip per account per cache window. See issue #455.
/// </summary>
public interface IAccountUserResolver
{
    ValueTask<int?> GetUserId(int accountId, CancellationToken cancellationToken = default);
}