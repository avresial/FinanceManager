using FinanceManager.Components.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.BondAccountComponents.TransactionHistory;

public partial class BondTransactionRow
{
    private bool _expanded;
    private bool _updateEntryVisibility;
    private bool _removeEntryVisibility;

    [Parameter] public required BondAccount Account { get; set; }
    [Parameter] public required BondAccountEntry Entry { get; set; }
    [Parameter] public BondDetails? BondDetails { get; set; }
    [Parameter] public bool IsMobile { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILogger<BondTransactionRow> Logger { get; set; }

    // Valuation figures for the detail view, mirroring the investment transaction row: the per-unit
    // price and the position's value, at posting time versus now, plus the gain/loss between them.
    // Bonds are priced with the domain's own accrual model (BondAccountEntry.GetPriceAt), so the
    // "now" figures carry accrued interest and no external market data is needed.
    private bool _showValuation;
    private decimal _unitPriceAtPosting;
    private decimal _unitPriceNow;
    private decimal _valueAtPosting;
    private decimal _valueNow;
    private decimal _gainLoss;
    private decimal _gainLossPercent;
    private string _valuationCurrency = string.Empty;

    protected override void OnParametersSet() => ComputeValuation();

    // Values the position held after this entry (Entry.Value units) at its posting-date price and at
    // today's accrued price. Entries can also reduce a position (negative ValueChange), so the figures
    // are labelled "at transaction" rather than "at purchase". Left hidden when there is no bond detail,
    // no priced units, or the accrual model cannot produce a positive figure (e.g. no calculation method
    // covers the dates), so the row never shows a misleading zero valuation or a -100% loss.
    private void ComputeValuation()
    {
        _showValuation = false;

        if (BondDetails is null || Entry.Value <= 0m || BondDetails.CalculationMethods.Count == 0)
            return;

        try
        {
            var postingDay = DateOnly.FromDateTime(Entry.PostingDate);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var valueAtPosting = Entry.GetPriceAt(postingDay, BondDetails);
            var valueNow = Entry.GetPriceAt(today, BondDetails);
            if (valueAtPosting <= 0m || valueNow <= 0m)
                return;

            _valueAtPosting = valueAtPosting;
            _valueNow = valueNow;
            _unitPriceAtPosting = valueAtPosting / Entry.Value;
            _unitPriceNow = valueNow / Entry.Value;
            _gainLoss = valueNow - valueAtPosting;
            _gainLossPercent = (valueNow / valueAtPosting - 1m) * 100m;
            _valuationCurrency = BondDetails.Currency.ShortName;
            _showValuation = true;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Could not compute bond valuation for entry {EntryId}", Entry.EntryId);
            _showValuation = false;
        }
    }

    private void ToggleExpanded() => _expanded = !_expanded;

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " " or "Spacebar")
            ToggleExpanded();
    }

    private string GetTitle() =>
        BondDetails is not null && !string.IsNullOrWhiteSpace(BondDetails.Name) ? BondDetails.Name : "Bond";

    private string GetAvatarLabel()
    {
        var source = BondDetails?.Name;
        if (string.IsNullOrWhiteSpace(source)) source = BondDetails?.Issuer;
        return string.IsNullOrWhiteSpace(source) ? "B" : source.Trim()[..1].ToUpperInvariant();
    }

    private static string FormatUnits(decimal value) => value.ToString("0.####");

    private Task ShowEditOverlay()
    {
        _updateEntryVisibility = true;
        return Task.CompletedTask;
    }

    private Task ShowRemoveOverlay()
    {
        _removeEntryVisibility = true;
        return Task.CompletedTask;
    }

    private Task HideOverlay()
    {
        _updateEntryVisibility = false;
        _removeEntryVisibility = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task ConfirmRemove()
    {
        _removeEntryVisibility = false;
        _expanded = false;

        try
        {
            await FinancialAccountService.RemoveEntry(Entry.EntryId, Account.AccountId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while removing entry");
        }

        await AccountDataSynchronizationService.AccountChanged();
        await InvokeAsync(StateHasChanged);
    }
}