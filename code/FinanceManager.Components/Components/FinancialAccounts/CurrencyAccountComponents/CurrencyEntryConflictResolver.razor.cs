using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.FinancialAccounts.CurrencyAccountComponents;

public partial class CurrencyEntryConflictResolver
{
    [Inject] public required CurrencyAccountImportHttpClient AccountImportHttpClient { get; set; }
    [Inject] public required ILogger<CurrencyEntryConflictResolver> Logger { get; set; }

    [Parameter] public IReadOnlyCollection<ImportConflict> Conflicts { get; set; } = [];
    [Parameter] public IReadOnlyCollection<ImportJobConflict>? JobConflicts { get; set; }
    [Parameter] public Guid? JobId { get; set; }
    [Parameter] public bool SkipExactMatches { get; set; } = true;
    [Parameter] public required string AccountName { get; set; }
    [Parameter] public EventCallback<IReadOnlyCollection<string>> OnConflictsResolved { get; set; }
    [Parameter] public EventCallback OnResolutionChanged { get; set; }

    private bool _isLoading = false;
    private string? _errorMessage;
    private int _accountId;
    private List<DayGroup> _dayGroups = [];
    private readonly Dictionary<DateTime, ResolveChoice> _dayChoices = new();

    public int ResolvedCount => _dayChoices.Count;
    public int UnresolvedCount => Math.Max(0, _dayGroups.Count - _dayChoices.Count);
    public int ImportedCount => _dayChoices.Values.Count(c => c == ResolveChoice.Imported);
    public int ExistingCount => _dayChoices.Values.Count(c => c == ResolveChoice.Existing);
    public bool AllResolved => _dayGroups.Count > 0 && _dayChoices.Count == _dayGroups.Count;

    protected override void OnParametersSet()
    {
        _isLoading = true;
        try
        {
            var source = JobConflicts is { Count: > 0 }
                ? JobConflicts
                    .Where(jc => !string.IsNullOrWhiteSpace(jc.ConflictId))
                    .Select(jc => new ResolverConflict(jc.Conflict, jc.ConflictId))
                : Conflicts
                    .Where(c => !string.IsNullOrWhiteSpace(c.ConflictId))
                    .Select(c => new ResolverConflict(c, c.ConflictId!));

            _dayGroups = source
                .Where(c => c.Conflict.ImportEntry is not null || c.Conflict.ExistingEntry is not null)
                .Where(c => !SkipExactMatches || !c.Conflict.IsExactMatch)
                .GroupBy(c => (c.Conflict.ImportEntry?.PostingDate ?? c.Conflict.ExistingEntry!.PostingDate).Date)
                .OrderBy(g => g.Key)
                .Select(g => new DayGroup(g.Key, g.ToList()))
                .ToList();

            if (_dayGroups.Count > 0)
                _accountId = _dayGroups[0].Conflicts[0].Conflict.AccountId;

            // Prune day choices for days that disappeared between renders.
            var presentDays = new HashSet<DateTime>(_dayGroups.Select(g => g.Date));
            foreach (var key in _dayChoices.Keys.Where(k => !presentDays.Contains(k)).ToList())
                _dayChoices.Remove(key);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing {Component} for account {AccountId}", nameof(CurrencyEntryConflictResolver), _accountId);
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
        var resolvedConflictIds = new List<string>();
        try
        {
            if (JobId.HasValue)
            {
                var decisions = new List<CurrencyImportConflictResolutionDecisionDto>();
                foreach (var g in _dayGroups)
                {
                    if (!_dayChoices.TryGetValue(g.Date, out var choice)) continue;
                    foreach (var c in g.Conflicts)
                    {
                        decisions.Add(new CurrencyImportConflictResolutionDecisionDto(
                            c.ConflictId,
                            choice == ResolveChoice.Imported,
                            choice == ResolveChoice.Existing));
                        resolvedConflictIds.Add(c.ConflictId);
                    }
                }

                if (decisions.Count == 0)
                {
                    _isLoading = false;
                    return;
                }

                var request = new CurrencyImportResolveConflictsRequestDto
                {
                    JobId = JobId.Value,
                    Decisions = decisions
                };
                await AccountImportHttpClient.ResolveAsyncCurrencyImportConflictsAsync(request);
                await OnConflictsResolved.InvokeAsync(resolvedConflictIds);
            }
            else
            {
                var resolvedImports = new List<ResolvedImportConflict>();
                foreach (var g in _dayGroups)
                {
                    if (!_dayChoices.TryGetValue(g.Date, out var choice)) continue;
                    foreach (var c in g.Conflicts)
                    {
                        resolvedImports.Add(new ResolvedImportConflict(
                            c.Conflict.AccountId,
                            choice == ResolveChoice.Imported,
                            c.Conflict.ImportEntry,
                            choice == ResolveChoice.Existing,
                            c.Conflict.ExistingEntry?.EntryId));
                    }
                }
                await AccountImportHttpClient.ResolveImportConflictsAsync(resolvedImports);
            }

            _dayChoices.Clear();
        }
        catch (Exception ex)
        {
            _errorMessage = "Could not submit conflict decisions. Please try again.";
            Logger.LogError(ex, "Error submitting conflict resolutions for account {AccountId}", _accountId);
        }
        _isLoading = false;
        StateHasChanged();
    }

    public enum ResolveChoice { Imported, Existing }

    private sealed record ResolverConflict(ImportConflict Conflict, string ConflictId);
    private sealed record DayGroup(DateTime Date, List<ResolverConflict> Conflicts);
}