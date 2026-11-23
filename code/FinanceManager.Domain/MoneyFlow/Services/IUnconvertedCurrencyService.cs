namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IUnconvertedCurrencyService
{
    /// <summary>
    /// The ISO codes of the currencies, other than <paramref name="displayCurrencyId"/>, whose cash balances or bond holdings the owner's
    /// net worth, assets and liabilities currently add up as they are, without converting them to the display currency.
    /// </summary>
    Task<IReadOnlyList<string>> GetCodes(int userId, int displayCurrencyId, CancellationToken cancellationToken = default);
}