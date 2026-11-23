using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Bond.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.Import;

public partial class BondEntryConflictResolver
{
    [Inject] public required BondAccountImportHttpClient AccountImportHttpClient { get; set; }
    [Inject] public required BondTransactionsHttpClient BondTransactionsHttpClient { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }
    [Inject] public required ILogger<BondEntryConflictResolver> Logger { get; set; }

    [Parameter] public required IReadOnlyCollection<BondImportConflict> Conflicts { get; set; }
    [Parameter] public bool SkipExactMatches { get; set; } = true;
    [Parameter] public required string AccountName { get; set; }

    /// <summary>
    /// Owner mode (Transactions page): every row of the import. The conflicts carry no account, so accepting a
    /// day re-submits all rows of the accepted keys and the server adds only the occurrences that are missing.
    /// </summary>
    [Parameter] public IReadOnlyList<BondEntryImportRecordDto>? OwnerRows { get; set; }

    [Parameter] public EventCallback OnResolutionChanged { get; set; }
    [Parameter] public EventCallback OnSubmitted { get; set; }

    private bool _isLoading;
    private string? _errorMessage;
    private List<DayGroup> _dayGroups = [];
    private readonly Dictionary<DateTime, ResolveChoice> _dayChoices = new();
    private Dictionary<int, string> _bondNamesById = [];

    public int ResolvedCount => _dayChoices.Count;
    public int UnresolvedCount => Math.Max(0, _dayGroups.Count - _dayChoices.Count);
    public int ImportedCount => _dayChoices.Values.Count(c => c == ResolveChoice.Imported);
    public int ExistingCount => _dayChoices.Values.Count(c => c == ResolveChoice.Existing);
    public bool AllResolved => _dayGroups.Count > 0 && _dayChoices.Count == _dayGroups.Count;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var bonds = await BondDetailsHttpClient.GetAll();
            _bondNamesById = bonds.ToDictionary(x => x.Id, x => x.Name);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to load bond details for name resolution");
        }
    }

    protected override void OnParametersSet()
    {
        _isLoading = true;
        try
        {
            _dayGroups = Conflicts
                .Where(c => c.ImportEntry is not null || c.ExistingEntry is not null)
                .Where(c => !SkipExactMatches || !c.IsExactMatch)
                .GroupBy(c => (c.ImportEntry?.PostingDate ?? c.ExistingEntry!.PostingDate).Date)
                .OrderBy(g => g.Key)
                .Select(g => new DayGroup(g.Key, g.ToList()))
                .ToList();


            var presentDays = new HashSet<DateTime>(_dayGroups.Select(g => g.Date));
            foreach (var key in _dayChoices.Keys.Where(k => !presentDays.Contains(k)).ToList())
                _dayChoices.Remove(key);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing {Component}", nameof(BondEntryConflictResolver));
        }
        _isLoading = false;
    }

    public ResolveChoice? GetDayChoice(DateTime date) =>
        _dayChoices.TryGetValue(date, out var c) ? c : null;

    public async Task PickDay(DateTime date, ResolveChoice choice)
    {
        _dayChoices[date] = choice;
        await OnResolutionChanged.InvokeAsync();
        StateHasChanged();
    }

    public async Task PickAll(ResolveChoice choice)
    {
        foreach (var g in _dayGroups)
            _dayChoices[g.Date] = choice;
        await OnResolutionChanged.InvokeAsync();
        StateHasChanged();
    }

    public async Task SubmitAsync()
    {
        if (_dayChoices.Count == 0)
            return;

        _isLoading = true;
        _errorMessage = null;
        try
        {
            var acceptedConflicts = _dayGroups
                .Where(g => _dayChoices.TryGetValue(g.Date, out var choice) && choice == ResolveChoice.Imported)
                .SelectMany(g => g.Conflicts)
                .ToList();

            if (OwnerRows is not null)
            {
                // "Existing" skips: owner mode never deletes existing entries.
                var acceptedKeys = acceptedConflicts
                    .Where(c => c.ImportEntry is not null)
                    .Select(c => (Truncate(c.ImportEntry!.PostingDate), c.ImportEntry.ValueChange, c.ImportEntry.BondDetailsId))
                    .ToHashSet();
                var acceptedRows = OwnerRows.Where(r => acceptedKeys.Contains((Truncate(r.PostingDate), r.ValueChange, r.BondDetailsId))).ToList();
                if (acceptedRows.Count > 0)
                    await BondTransactionsHttpClient.ResolveImportAsync(acceptedRows);
            }
            else
            {
                // The account import review only handles conflicts that carry an account.
                var resolvedImports = acceptedConflicts
                    .Where(c => c.AccountId is not null)
                    .Select(c => new ResolvedBondImportConflict(c.AccountId!.Value, true, c.ImportEntry, false, c.ExistingEntry?.EntryId))
                    .ToList();
                if (resolvedImports.Count > 0)
                    await AccountImportHttpClient.ResolveImportConflictsAsync(resolvedImports);
            }

            _dayChoices.Clear();
            await OnSubmitted.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = "Could not submit conflict decisions. Please try again.";
            Logger.LogError(ex, "Error submitting conflict resolutions");
        }
        _isLoading = false;
        StateHasChanged();
    }

    // Posting dates match at second precision, as the conflict detection compares them.
    private static DateTime Truncate(DateTime d) => new(d.Year, d.Month, d.Day, d.Hour, d.Minute, d.Second);

    public string GetBondName(int bondDetailsId) =>
        _bondNamesById.TryGetValue(bondDetailsId, out var name) ? name : $"Bond #{bondDetailsId}";

    public enum ResolveChoice { Imported, Existing }

    private sealed record DayGroup(DateTime Date, List<BondImportConflict> Conflicts);
}