using FinanceManager.Components.Features.Identity.HttpClients;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.GiftCodes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class AdminGiftCodes : ComponentBase
{
    private const int _pageSize = 50;
    private readonly List<string> _errors = [];
    private List<GiftCodeDto> _giftCodes = [];
    private PricingLevel _pricingLevel = PricingLevel.Basic;
    private string? _note;
    private GeneratedGiftCode? _generated;
    private string? _successMessage;
    private bool _isLoading;
    private bool _isBusy;
    private bool _hasNextPage;
    private int _page;

    [Inject] public required GiftCodeHttpClient GiftCodeHttpClient { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    [Inject] public required ILogger<AdminGiftCodes> Logger { get; set; }

    protected override Task OnInitializedAsync() => LoadPageAsync();

    private async Task LoadPageAsync()
    {
        if (_isBusy || _isLoading) return;
        _isBusy = true;
        _isLoading = true;
        _errors.Clear();
        try
        {
            _giftCodes = await GiftCodeHttpClient.GetAdminGiftCodes(_page * _pageSize, _pageSize) ?? [];
            _hasNextPage = _giftCodes.Count == _pageSize;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load gift code audit page {Page}", _page + 1);
            _errors.Add("Gift code history could not be loaded. Please try again.");
        }
        finally
        {
            _isLoading = false;
            _isBusy = false;
        }
    }

    private async Task GenerateAsync()
    {
        if (_isBusy || _pricingLevel is not (PricingLevel.Basic or PricingLevel.Premium)) return;
        _errors.Clear();
        _successMessage = null;
        _generated = null;
        _isBusy = true;
        try
        {
            _generated = await GiftCodeHttpClient.Generate(new GenerateGiftCode(_pricingLevel, _note?.Trim()));
            _note = null;
            _successMessage = $"Generated a lifetime {_pricingLevel} gift code.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate a {PricingLevel} gift code", _pricingLevel);
            _errors.Add("Gift code could not be generated. Please try again.");
            _isBusy = false;
            return;
        }
        _page = 0;
        try
        {
            await LoadPageWhileBusyAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Gift code was generated, but the audit list could not be refreshed");
            _errors.Add("The code was generated, but gift code history could not be refreshed.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task LoadPageWhileBusyAsync()
    {
        _giftCodes = await GiftCodeHttpClient.GetAdminGiftCodes(_page * _pageSize, _pageSize) ?? [];
        _hasNextPage = _giftCodes.Count == _pageSize;
    }

    private async Task CopyCodeAsync()
    {
        if (_generated is null) return;
        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", _generated.Code);
            _successMessage = "Gift code copied.";
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not copy a generated gift code to the clipboard");
            _errors.Add("Clipboard access failed. Select and copy the code manually.");
        }
    }

    private async Task RevokeAsync(GiftCodeDto giftCode)
    {
        if (_isBusy || giftCode.State != GiftCodeState.Active) return;
        var confirmed = await DialogService.ShowMessageBoxAsync("Revoke gift code",
            $"Revoke gift code #{giftCode.Id} ending in {giftCode.CodeSuffix}? Its lifetime {giftCode.PricingLevel} upgrade will no longer be redeemable.",
            yesText: "Revoke", cancelText: "Cancel");
        if (confirmed != true) return;
        if (_isBusy || giftCode.State != GiftCodeState.Active) return;

        _errors.Clear();
        _successMessage = null;
        _isBusy = true;
        try
        {
            using var response = await GiftCodeHttpClient.Revoke(giftCode.Id);
            if (response.IsSuccessStatusCode)
            {
                _successMessage = $"Gift code ending in {giftCode.CodeSuffix} was revoked.";
                try
                {
                    await LoadPageWhileBusyAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Gift code {GiftCodeId} was revoked, but the audit list could not be refreshed", giftCode.Id);
                    _errors.Add("The code was revoked, but gift code history could not be refreshed.");
                }
            }
            else if ((int)response.StatusCode == 409)
            {
                _errors.Add("This gift code is no longer active and cannot be revoked.");
                try
                {
                    await LoadPageWhileBusyAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not refresh history after gift code {GiftCodeId} could not be revoked", giftCode.Id);
                    _errors.Add("Gift code history could not be refreshed.");
                }
            }
            else
            {
                Logger.LogWarning("Gift code revocation failed with status {StatusCode} for gift code {GiftCodeId}", response.StatusCode, giftCode.Id);
                _errors.Add("Gift code could not be revoked. Please refresh and try again.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to revoke gift code {GiftCodeId}", giftCode.Id);
            _errors.Add("Gift code could not be revoked. Please try again.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task PreviousPageAsync()
    {
        if (_isBusy || _page == 0) return;
        _page--;
        await LoadPageAsync();
    }

    private async Task NextPageAsync()
    {
        if (_isBusy || !_hasNextPage) return;
        _page++;
        await LoadPageAsync();
    }

    private static Color GetStateColor(GiftCodeState state) => state switch
    {
        GiftCodeState.Active => Color.Success,
        GiftCodeState.Redeemed => Color.Info,
        GiftCodeState.Revoked => Color.Error,
        _ => Color.Default
    };

    private static string FormatMetadata(DateTime atUtc, int? userId) =>
        $"{atUtc.ToLocalTime():g} · user {userId?.ToString() ?? "unknown"}";
}