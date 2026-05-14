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

    private bool _isLoading = false;
    private string? _errorMessage;
    private int AccountId { get; set; }
    private DateTime? _selectedDay = null;
    private List<ResolverConflict> _selectedConflicts = [];
    private Dictionary<DateTime, List<ResolverConflict>> _conflictsByDay = [];
    private ConflictPanel? _hoveredPanel;

    protected override void OnParametersSet()
    {
        _isLoading = true;
        try
        {
            base.OnParametersSet();

            var selectedDay = _selectedDay;
            _conflictsByDay.Clear();
            _selectedConflicts = [];

            var source = JobConflicts is { Count: > 0 }
                ? JobConflicts.Select(x => new ResolverConflict(x.Conflict, x.ConflictId))
                : Conflicts.Select(x => new ResolverConflict(x, x.ConflictId));

            _conflictsByDay = source
                .Where(c => c.Conflict.ImportEntry is not null || c.Conflict.ExistingEntry is not null)
                .GroupBy(c => (c.Conflict.ImportEntry?.PostingDate ?? c.Conflict.ExistingEntry!.PostingDate).Date)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (SkipExactMatches)
            {
                var keysToRemove = _conflictsByDay.Where(x => x.Value.All(y => y.Conflict.IsExactMatch))
                                        .Select(x => x.Key)
                                        .ToList();

                foreach (var key in keysToRemove)
                    _conflictsByDay.Remove(key);
            }

            if (_conflictsByDay.Count != 0)
            {
                _selectedDay = selectedDay.HasValue && _conflictsByDay.ContainsKey(selectedDay.Value)
                    ? selectedDay.Value
                    : _conflictsByDay.Keys.OrderBy(k => k).First();
                _selectedConflicts = _selectedDay.HasValue ? _conflictsByDay[_selectedDay.Value] : [];
                AccountId = _selectedConflicts.First().Conflict.AccountId;
            }
            else
            {
                _selectedDay = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing {CurrencyEntryConflictResolver} for account {AccountId}", nameof(CurrencyEntryConflictResolver), AccountId);
        }
        _isLoading = false;
    }

    private async Task OnPickImported()
    {
        _isLoading = true;
        _errorMessage = null;
        var selectedConflicts = _selectedConflicts.ToList();
        var snapshot = CreateSnapshot();
        RemoveSelectedDayAndAdvance();
        try
        {
            if (JobId.HasValue)
            {
                var pickedConflictIds = selectedConflicts
                    .Select(x => x.ConflictId)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToList();

                var request = new CurrencyImportResolveConflictsRequestDto
                {
                    JobId = JobId.Value,
                    Decisions = pickedConflictIds
                        .Select(id => new CurrencyImportConflictResolutionDecisionDto(id, true, false))
                        .ToList()
                };

                if (request.Decisions.Count != 0)
                    await AccountImportHttpClient.ResolveAsyncCurrencyImportConflictsAsync(request);

                if (pickedConflictIds.Count != 0)
                    await OnConflictsResolved.InvokeAsync(pickedConflictIds);
            }
            else
            {
                var resolvedImports = selectedConflicts
                    .Select(c => new ResolvedImportConflict(c.Conflict.AccountId, true, c.Conflict.ImportEntry, false, c.Conflict.ExistingEntry?.EntryId))
                    .ToList();

                await AccountImportHttpClient.ResolveImportConflictsAsync(resolvedImports);
            }
        }
        catch (Exception ex)
        {
            RestoreSnapshot(snapshot);
            _errorMessage = "Could not resolve this conflict. Please try again.";
            Logger.LogError(ex, "Error resolving import conflicts for account {AccountId}", AccountId);
        }
        _isLoading = false;
    }

    private async Task OnPickExisting()
    {
        _isLoading = true;
        _errorMessage = null;
        var selectedConflicts = _selectedConflicts.ToList();
        var snapshot = CreateSnapshot();
        RemoveSelectedDayAndAdvance();
        try
        {
            if (JobId.HasValue)
            {
                var pickedConflictIds = selectedConflicts
                    .Select(x => x.ConflictId)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToList();

                var request = new CurrencyImportResolveConflictsRequestDto
                {
                    JobId = JobId.Value,
                    Decisions = pickedConflictIds
                        .Select(id => new CurrencyImportConflictResolutionDecisionDto(id, false, true))
                        .ToList()
                };

                if (request.Decisions.Count != 0)
                    await AccountImportHttpClient.ResolveAsyncCurrencyImportConflictsAsync(request);

                if (pickedConflictIds.Count != 0)
                    await OnConflictsResolved.InvokeAsync(pickedConflictIds);
            }
        }
        catch (Exception ex)
        {
            RestoreSnapshot(snapshot);
            _errorMessage = "Could not resolve this conflict. Please try again.";
            Logger.LogError(ex, "Error resolving import conflicts for account {AccountId}", AccountId);
        }
        _isLoading = false;
    }

    private void RemoveSelectedDayAndAdvance()
    {
        if (_selectedDay is null) return;

        var key = _selectedDay.Value;
        _conflictsByDay.Remove(key);
        if (_conflictsByDay.Count == 0)
        {
            _selectedDay = null;
            _selectedConflicts = [];
            return;
        }

        var next = _conflictsByDay.Keys.OrderBy(k => k).First();
        _selectedDay = next;
        _selectedConflicts = _conflictsByDay[next];
    }

    private ResolverStateSnapshot CreateSnapshot() =>
        new(
            _selectedDay,
            _selectedConflicts.ToList(),
            _conflictsByDay.ToDictionary(x => x.Key, x => x.Value.ToList()));

    private void RestoreSnapshot(ResolverStateSnapshot snapshot)
    {
        _selectedDay = snapshot.SelectedDay;
        _selectedConflicts = snapshot.SelectedConflicts;
        _conflictsByDay = snapshot.ConflictsByDay;
    }

    private void SetHoveredPanel(ConflictPanel? panel) => _hoveredPanel = panel;

    private string GetPanelStyle(ConflictPanel panel)
    {
        var isHovered = _hoveredPanel == panel;
        var background = isHovered
            ? "color-mix(in srgb, var(--mud-palette-primary) 10%, transparent)"
            : "rgba(255,255,255,0.02)";
        var border = isHovered
            ? "color-mix(in srgb, var(--mud-palette-primary) 50%, transparent)"
            : "rgba(255,255,255,0.08)";

        return $"background-color: {background}; border: 1px solid {border}; cursor: pointer; transition: background-color 160ms ease, border-color 160ms ease;";
    }

    private sealed record ResolverConflict(ImportConflict Conflict, string? ConflictId);
    private sealed record ResolverStateSnapshot(
        DateTime? SelectedDay,
        List<ResolverConflict> SelectedConflicts,
        Dictionary<DateTime, List<ResolverConflict>> ConflictsByDay);

    private enum ConflictPanel
    {
        Imported,
        Existing
    }
}
