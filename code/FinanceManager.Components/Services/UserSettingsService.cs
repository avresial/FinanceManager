using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

/// <summary>
/// Resolves the logged-in user's preferred currency once per app lifetime and hands it to every
/// component that presents money, so all assets are recalculated to that currency.
/// </summary>
public class UserSettingsService(
    ILoginService loginService,
    IUserService userService,
    CurrencyHttpClient currencyHttpClient,
    ILogger<UserSettingsService> logger) : ISettingsService
{
    private Currency? _cachedCurrency;

    public Currency GetCurrency() => _cachedCurrency ?? DefaultCurrency.PLN;

    public async Task<Currency> GetCurrencyAsync()
    {
        if (_cachedCurrency is not null) return _cachedCurrency;

        try
        {
            var loggedUser = await loginService.GetLoggedUser();
            if (loggedUser is null) return DefaultCurrency.PLN;

            var user = await userService.GetUser(loggedUser.UserId);
            if (user is null) return DefaultCurrency.PLN;

            var currencies = await currencyHttpClient.GetAll();
            _cachedCurrency = currencies.FirstOrDefault(x => x.Id == user.PreferredCurrencyId) ?? DefaultCurrency.PLN;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve the user's preferred currency; falling back to {Currency}", DefaultCurrency.PLN.ShortName);
            return DefaultCurrency.PLN;
        }

        return _cachedCurrency;
    }

    /// <summary>Refreshes the cached currency after the user changed the preference in settings.</summary>
    public void SetCurrency(Currency currency) => _cachedCurrency = currency;
}