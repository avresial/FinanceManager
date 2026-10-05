using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.Identity.HttpClients;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.GiftCodes;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Text.Json;

namespace FinanceManager.Components.Features.Identity.Components;

public partial class UserSettingsPage : ComponentBase, IAsyncDisposable
{
    private const string _scrollContainerSelector = "html";
    private const string _sectionClass = "settings-section";
    private const string _requiredDeleteConfirmation = "delete my account";

    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _info = [];

    private UserSession? _loggedUser;
    private Domain.Identity.Entities.User? _userData;
    private RecordCapacity? _recordCapacity;

    private bool _isLoadingPage;
    private IScrollSpy _scrollSpy = null!;
    private bool _isSavingPreferences;
    private bool _isSavingPassword;
    private bool _scrollSpyStarted;

    private string _displayName = string.Empty;
    private string _email = string.Empty;

    private string? _currentPassword;
    private string? _newPassword;
    private string? _confirmPassword;
    private int _passwordFormVersion;

    private string _giftCode = string.Empty;
    private bool _isRedeemingGiftCode;
    private List<Currency> _currencies = [];
    private int _selectedCurrencyId = DefaultCurrency.PLN.Id;
    private int _initialCurrencyId = DefaultCurrency.PLN.Id;
    private InstrumentSearchResultDto? _selectedBenchmark;
    private InstrumentSearchResultDto? _initialBenchmark;
    private string? _selectedSection = "profile";
    private string? _deleteConfirmation;

    [Inject] public required IUserService UserService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyHttpClient { get; set; }
    [Inject] public required InvestmentTransactionHttpClient InvestmentTransactionHttpClient { get; set; }
    [Inject] public required UserSettingsService UserSettingsService { get; set; }
    [Inject] public required GiftCodeHttpClient GiftCodeHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }
    [Inject] public required IScrollSpyFactory ScrollSpyFactory { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILogger<UserSettingsPage> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _scrollSpy = ScrollSpyFactory.Create();
        _isLoadingPage = true;
        _loggedUser = await LoginService.GetLoggedUser();
        if (_loggedUser is null)
        {
            _isLoadingPage = false;
            return;
        }

        _userData = await UserService.GetUser(_loggedUser.UserId);
        if (_userData is null)
        {
            _isLoadingPage = false;
            return;
        }

        _displayName = UserDisplayName.From(_userData);
        _email = _userData.Login;

        try
        {
            _currencies = await CurrencyHttpClient.GetAll();
        }
        catch (Exception ex)
        {
            _errors.Insert(0, ex.Message);
        }

        _selectedCurrencyId = _currencies.Any(x => x.Id == _userData.PreferredCurrencyId)
            ? _userData.PreferredCurrencyId
            : DefaultCurrency.PLN.Id;
        _initialCurrencyId = _selectedCurrencyId;
        _selectedBenchmark = await UserSettingsService.GetBenchmarkAsync();
        _initialBenchmark = _selectedBenchmark;

        try
        {
            _recordCapacity = await UserService.GetRecordCapacity(_loggedUser.UserId);
        }
        catch (Exception ex)
        {
            _errors.Insert(0, ex.Message);
        }
        _isLoadingPage = false;
    }

    private bool CanChangePassword =>
        !string.IsNullOrEmpty(_currentPassword)
        && !string.IsNullOrEmpty(_newPassword)
        && string.Equals(_newPassword, _confirmPassword, StringComparison.Ordinal)
        && !PasswordStrength(_newPassword).Any();

    private bool HasPreferenceChanges =>
        _selectedCurrencyId != _initialCurrencyId
        || _selectedBenchmark?.ListingId != _initialBenchmark?.ListingId;

    private async Task OnSectionSelectedAfter()
    {
        if (string.IsNullOrEmpty(_selectedSection)) return;
        await _scrollSpy.ScrollToSection(_selectedSection);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_userData is null || _scrollSpyStarted) return;

        _scrollSpyStarted = true;
        _scrollSpy.ScrollSectionSectionCentered += OnSectionCentered;
        await _scrollSpy.StartSpying(_scrollContainerSelector, _sectionClass);
    }

    private void OnSectionCentered(object? sender, ScrollSectionCenteredEventArgs args)
    {
        if (args.Id == _selectedSection) return;
        _selectedSection = args.Id;
        _ = InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        _scrollSpy.ScrollSectionSectionCentered -= OnSectionCentered;
        await _scrollSpy.DisposeAsync();
    }

    private async Task SavePreferencesAsync()
    {
        if (_loggedUser is null || _isSavingPreferences) return;

        _errors.Clear();
        _isSavingPreferences = true;
        try
        {
            if (_selectedCurrencyId != _initialCurrencyId)
                await UpdatePreferredCurrency();
            if (_selectedBenchmark?.ListingId != _initialBenchmark?.ListingId)
                await UpdatePreferredBenchmark();
        }
        finally
        {
            _isSavingPreferences = false;
        }

        if (_errors.Count == 0)
            Snackbar.Add("Preferences saved.", Severity.Success);
    }

    private async Task SavePasswordAsync()
    {
        if (_loggedUser is null || _isSavingPassword || !CanChangePassword) return;

        _errors.Clear();
        _warnings.Clear();
        _isSavingPassword = true;
        try
        {
            var changed = await UserService.UpdatePassword(_loggedUser.UserId, _newPassword!, _currentPassword);
            if (!changed)
            {
                _errors.Insert(0, "Failed to change password. Check that your current password is correct.");
                return;
            }

            _currentPassword = null;
            _newPassword = null;
            _confirmPassword = null;
            _passwordFormVersion++;
            Snackbar.Add("Password changed.", Severity.Success);
        }
        finally
        {
            _isSavingPassword = false;
        }
    }

    private string PasswordMatch(string arg)
    {
        if (!string.Equals(_newPassword, arg, StringComparison.Ordinal))
            return "Passwords don't match";

        return string.Empty;
    }

    private static IEnumerable<string> PasswordStrength(string pw)
    {
        if (string.IsNullOrWhiteSpace(pw))
        {
            yield return "Password is required!";
            yield break;
        }

#if DEBUG
        yield break;
#else
        if (pw.Length < 8)
            yield return "Password must be at least of length 8";
        if (!System.Text.RegularExpressions.Regex.IsMatch(pw, @"[A-Z]"))
            yield return "Password must contain at least one capital letter";
        if (!System.Text.RegularExpressions.Regex.IsMatch(pw, @"[a-z]"))
            yield return "Password must contain at least one lowercase letter";
        if (!System.Text.RegularExpressions.Regex.IsMatch(pw, @"[0-9]"))
            yield return "Password must contain at least one digit";
#endif
    }

    private async Task RedeemGiftCode()
    {
        if (_loggedUser is null || _userData is null || _isRedeemingGiftCode || string.IsNullOrWhiteSpace(_giftCode)) return;

        _errors.Clear();
        _warnings.Clear();
        _info.Clear();
        _isRedeemingGiftCode = true;
        try
        {
            using var response = await GiftCodeHttpClient.Redeem(_giftCode.Trim());
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 429)
                {
                    _errors.Insert(0, "Too many redemption attempts. Please wait a few minutes before trying again.");
                    return;
                }
                var error = await ReadRedemptionErrorAsync(response.Content);
                _errors.Insert(0, string.IsNullOrWhiteSpace(error)
                    ? "This gift code could not be redeemed. Check the code and try again."
                    : error);
                return;
            }

            _giftCode = string.Empty;
            var refreshedUser = await UserService.GetUser(_loggedUser.UserId);
            if (refreshedUser is null)
            {
                _errors.Insert(0, "Gift code redeemed, but your account could not be refreshed. Reload the page to see your new plan.");
                return;
            }

            _userData = refreshedUser;
            UserService.NotifyUserChanged(refreshedUser);
            _recordCapacity = await UserService.GetRecordCapacity(_loggedUser.UserId);
            _info.Insert(0, $"Gift code redeemed. Your lifetime {refreshedUser.PricingLevel} plan is now active.");
        }
        catch (Exception ex)
        {
            _errors.Insert(0, "Could not confirm redemption. Refresh the page to check your tier before trying again.");
            // Code values are deliberately excluded from logs.
            Logger.LogError(ex, "Failed to redeem gift code for user {UserId}", _loggedUser.UserId);
        }
        finally
        {
            _isRedeemingGiftCode = false;
        }
    }

    private static async Task<string?> ReadRedemptionErrorAsync(HttpContent content)
    {
        var body = await content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body)) return null;

        if (content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.String)
                    return document.RootElement.GetString();
                if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
                if (document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    return detail.GetString();
                if (document.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    return title.GetString();
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        return body;
    }

    private async Task UpdatePreferredCurrency()
    {
        if (_loggedUser is null) return;

        var result = await UserService.UpdatePreferredCurrency(_loggedUser.UserId, _selectedCurrencyId);
        if (!result)
        {
            _errors.Insert(0, "Failed to change preferred currency.");
            return;
        }

        _initialCurrencyId = _selectedCurrencyId;
        var selectedCurrency = _currencies.FirstOrDefault(x => x.Id == _selectedCurrencyId);
        if (selectedCurrency is not null)
        {
            UserSettingsService.SetCurrency(selectedCurrency);
            _info.Insert(0, $"Preferred currency changed to {selectedCurrency.ShortName}.");
        }
    }

    private async Task UpdatePreferredBenchmark()
    {
        if (_loggedUser is null) return;

        var result = await UserService.UpdatePreferredBenchmark(_loggedUser.UserId, _selectedBenchmark?.ListingId);
        if (!result)
        {
            _errors.Insert(0, "Failed to change investment benchmark.");
            return;
        }

        _initialBenchmark = _selectedBenchmark;
        UserSettingsService.SetBenchmark(_selectedBenchmark);
        _info.Insert(0, _selectedBenchmark is null
            ? "Investment benchmark changed to Polish inflation."
            : $"Investment benchmark changed to {_selectedBenchmark.Ticker}.");
    }

    private void OnBenchmarkChanged(InstrumentSearchResultDto? benchmark)
    {
        _selectedBenchmark = benchmark;
    }

    private async Task<IEnumerable<InstrumentSearchResultDto>> SearchBenchmarksAsync(
        string value,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        return await InvestmentTransactionHttpClient.SearchListingsAsync(
            value,
            cancellationToken: cancellationToken);
    }

    private async Task DeleteMyAccount()
    {
        if (_loggedUser is null) return;
        var result = await UserService.Delete(_loggedUser.UserId);
        if (!result)
        {
            _errors.Insert(0, "Failed to remove user.");
            return;
        }

        _errors.Clear();
        await LoginService.Logout();
        NavigationManager.NavigateTo("/");
    }

    private Color GetStorageIndicatorColor()
    {
        if (_recordCapacity is not null && _recordCapacity.GetStorageUsedPercentage() >= 80)
            return Color.Error;
        return Color.Primary;
    }

}