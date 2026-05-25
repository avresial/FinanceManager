using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Imports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.FinancialAccounts.BondAccountComponents;

public partial class BondEntryConflictResolver
{
    [Inject] public required BondAccountImportHttpClient AccountImportHttpClient { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }
    [Inject] public required ILogger<BondEntryConflictResolver> Logger { get; set; }

    [Parameter] public required IReadOnlyCollection<BondImportConflict> Conflicts { get; set; }
    [Parameter] public bool SkipExactMatches { get; set; } = true;
    [Parameter] public required string AccountName { get; set; }
    [Parameter] public EventCallback OnResolutionChanged { get; set; }
    [Parameter] public EventCallback OnSubmitted { get; set; }

    private bool _isLoading;
    private string? _errorMessage;
    private int _accountId;
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

            if (_dayGroups.Count > 0)
                _accountId = _dayGroups[0].Conflicts[0].AccountId;

            var presentDays = new HashSet<DateTime>(_dayGroups.Select(g => g.Date));
            foreach (var key in _dayChoices.Keys.Where(k => !presentDays.Contains(k)).ToList())
                _dayChoices.Remove(key);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing {Component} for account {AccountId}", nameof(BondEntryConflictResolver), _accountId);
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
            var resolvedImports = new List<ResolvedBondImportConflict>();
            foreach (var g in _dayGroups)
            {
                if (!_dayChoices.TryGetValue(g.Date, out var choice)) continue;
                if (choice != ResolveChoice.Imported) continue;
                foreach (var c in g.Conflicts)
                {
                    resolvedImports.Add(new ResolvedBondImportConflict(
                        c.AccountId, true, c.ImportEntry, false, c.ExistingEntry?.EntryId));
                }
            }

            if (resolvedImports.Count > 0)
                await AccountImportHttpClient.ResolveImportConflictsAsync(resolvedImports);

            _dayChoices.Clear();
            await OnSubmitted.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = "Could not submit conflict decisions. Please try again.";
            Logger.LogError(ex, "Error submitting conflict resolutions for account {AccountId}", _accountId);
        }
        _isLoading = false;
        StateHasChanged();
    }

    public string GetBondName(int bondDetailsId) =>
        _bondNamesById.TryGetValue(bondDetailsId, out var name) ? name : $"Bond #{bondDetailsId}";

    public enum ResolveChoice { Imported, Existing }

    private sealed record DayGroup(DateTime Date, List<BondImportConflict> Conflicts);
}