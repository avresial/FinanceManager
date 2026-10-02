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
using Microsoft.JSInterop;
using MudBlazor;
using System.Text.Json;

namespace FinanceManager.Components.Features.Identity.Components;

public partial class UserSettingsPage : ComponentBase
{
    private const string _requiredDeleteConfirmation = "delete my account";

    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _info = [];

    private UserSession? _loggedUser;
    private Domain.Identity.Entities.User? _userData;
    private RecordCapacity? _recordCapacity;

    private bool _isLoadingPage;
    private bool _isDirty;
    private bool _passwordValid;

    private string _displayName = string.Empty;
    private string _email = string.Empty;
    private string _initialDisplayName = string.Empty;

    private string? _currentPassword;
    private string? _newPassword;
    private string? _confirmPassword;
    private MudForm? _passwordForm;
    private MudTextField<string>? _passwordField;

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
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required ILogger<UserSettingsPage> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
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

        _displayName = _userData.Login;
        _email = _userData.Login;
        _initialDisplayName = _displayName;

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

    private void MarkDirty() => _isDirty = HasChanges();

    private bool HasChanges()
    {
        if (_displayName != _initialDisplayName) return true;
        if (_selectedCurrencyId != _initialCurrencyId) return true;
        if (_selectedBenchmark?.ListingId != _initialBenchmark?.ListingId) return true;
        if (!string.IsNullOrEmpty(_currentPassword)) return true;
        if (!string.IsNullOrEmpty(_newPassword)) return true;
        if (!string.IsNullOrEmpty(_confirmPassword)) return true;
        return false;
    }

    private async Task OnSectionSelectedAfter()
    {
        if (string.IsNullOrEmpty(_selectedSection)) return;
        await JSRuntime.InvokeVoidAsync("eval", $"document.getElementById('{_selectedSection}')?.scrollIntoView({{ behavior: 'smooth', block: 'start' }})");
    }

    private async Task SaveAll()
    {
        if (_loggedUser is null) return;

        _errors.Clear();
        _warnings.Clear();
        _info.Clear();

        var hasPasswordChange = !string.IsNullOrEmpty(_newPassword) || !string.IsNullOrEmpty(_confirmPassword) || !string.IsNullOrEmpty(_currentPassword);
        if (hasPasswordChange)
        {
            await ChangePasswordAsync();
        }

        if (_selectedCurrencyId != _initialCurrencyId)
        {
            await UpdatePreferredCurrency();
        }

        if (_selectedBenchmark?.ListingId != _initialBenchmark?.ListingId)
        {
            await UpdatePreferredBenchmark();
        }

        _initialDisplayName = _displayName;
        _isDirty = HasChanges();
    }

    private void DiscardChanges()
    {
        _displayName = _initialDisplayName;
        _selectedCurrencyId = _initialCurrencyId;
        _selectedBenchmark = _initialBenchmark;
        _currentPassword = null;
        _newPassword = null;
        _confirmPassword = null;
        if (_passwordField is not null)
            _ = _passwordField.ResetAsync();
        _errors.Clear();
        _warnings.Clear();
        _info.Clear();
        _isDirty = false;
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
        MarkDirty();
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

    private async Task ChangePasswordAsync()
    {
        if (_loggedUser is null) return;
        if (_passwordForm is null) return;
        if (_passwordField is null) return;
        if (string.IsNullOrEmpty(_confirmPassword)) return;

        await _passwordForm.Validate();
        if (!_passwordForm.IsValid) return;

        if (!string.Equals(_newPassword, _confirmPassword, StringComparison.Ordinal))
        {
            _warnings.Insert(0, "New passwords do not match.");
            return;
        }

        if (string.IsNullOrEmpty(_currentPassword))
        {
            _warnings.Insert(0, "Current password is required.");
            return;
        }

        var result = await UserService.UpdatePassword(_loggedUser.UserId, _confirmPassword, _currentPassword);
        if (!result)
        {
            _errors.Insert(0, "Failed to change password. Check that your current password is correct.");
            return;
        }

        _info.Insert(0, "Password changed successfully.");
        _currentPassword = null;
        _newPassword = null;
        _confirmPassword = null;
        await _passwordField.ResetAsync();
    }

    private Color GetStorageIndicatorColor()
    {
        if (_recordCapacity is not null && _recordCapacity.GetStorageUsedPercentage() >= 80)
            return Color.Error;
        return Color.Primary;
    }

}