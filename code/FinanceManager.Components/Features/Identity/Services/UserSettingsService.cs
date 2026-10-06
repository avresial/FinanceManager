using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Identity.Services;

/// <summary>
/// Resolves the logged-in user's preferred currency once per app lifetime and hands it to every
/// component that presents money, so all assets are recalculated to that currency.
/// </summary>
public class UserSettingsService(
    ILoginService loginService,
    IUserService userService,
    CurrencyHttpClient currencyHttpClient,
    InvestmentTransactionHttpClient investmentTransactionHttpClient,
    ILogger<UserSettingsService> logger) : ISettingsService
{
    private Currency? _cachedCurrency;
    private Task<Currency>? _currencyResolution;
    private bool _benchmarkLoaded;
    private InstrumentSearchResultDto? _cachedBenchmark;

    public Currency GetCurrency() => _cachedCurrency ?? DefaultCurrency.PLN;

    public Task<Currency> GetCurrencyAsync()
    {
        if (_cachedCurrency is not null) return Task.FromResult(_cachedCurrency);

        // Share one in-flight resolution: the layout and the first page ask at the same time on startup. #890
        if (_currencyResolution is not null) return _currencyResolution;

        var resolution = ResolveCurrencyAsync();
        if (!resolution.IsCompleted)
            _currencyResolution = resolution;

        return resolution;
    }

    private async Task<Currency> ResolveCurrencyAsync()
    {
        try
        {
            var loggedUser = await loginService.GetLoggedUser();
            if (loggedUser is null) return DefaultCurrency.PLN;

            var user = await userService.GetUser(loggedUser.UserId);
            if (user is null) return DefaultCurrency.PLN;

            var currencies = await currencyHttpClient.GetAll();
            _cachedCurrency = currencies.FirstOrDefault(x => x.Id == user.PreferredCurrencyId) ?? DefaultCurrency.PLN;
            return _cachedCurrency;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve the user's preferred currency; falling back to {Currency}", DefaultCurrency.PLN.ShortName);
            return DefaultCurrency.PLN;
        }
        finally
        {
            _currencyResolution = null;
        }
    }

    /// <summary>Refreshes the cached currency after the user changed the preference in settings.</summary>
    public void SetCurrency(Currency currency) => _cachedCurrency = currency;

    public async Task<InstrumentSearchResultDto?> GetBenchmarkAsync()
    {
        if (_benchmarkLoaded) return _cachedBenchmark;

        try
        {
            var loggedUser = await loginService.GetLoggedUser();
            var user = loggedUser is null ? null : await userService.GetUser(loggedUser.UserId);
            if (user?.PreferredBenchmarkListingId is long listingId)
                _cachedBenchmark = await investmentTransactionHttpClient.GetListingAsync(listingId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve the user's preferred investment benchmark");
        }

        _benchmarkLoaded = true;
        return _cachedBenchmark;
    }

    public void SetBenchmark(InstrumentSearchResultDto? benchmark)
    {
        _cachedBenchmark = benchmark;
        _benchmarkLoaded = true;
    }
}