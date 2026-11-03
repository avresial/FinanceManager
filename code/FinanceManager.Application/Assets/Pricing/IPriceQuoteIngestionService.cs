using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Application.Assets.Pricing;

public interface IPriceQuoteIngestionService
{
    /// <summary>
    /// Fetches end-of-day quotes for <paramref name="listing"/> over the window from its enabled
    /// market-data symbols (first source that returns data wins) and stores them. Does nothing
    /// when the window is already covered or contains no trading day.
    /// </summary>
    Task FetchAndStoreAsync(AssetListing listing, DateTime start, DateTime end, CancellationToken ct = default);
}