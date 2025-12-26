using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Imports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.FinancialAccounts.BankAccountComponents;

public partial class BankEntryConflictResolver
{
    [Inject] public required BankAccountImportHttpClient BankAccountImportHttpClient { get; set; }
    [Inject] public required ILogger<BankEntryConflictResolver> Logger { get; set; }

    [Parameter] public required IReadOnlyCollection<ImportConflict> Conflicts { get; set; }
    [Parameter] public required bool SkipExactMatches { get; set; } = true;
    [Parameter] public required string AccountName { get; set; }

    private bool _isLoading = false;
    private int AccountId { get; set; }
    private DateTime? _selectedDay = null;
    private List<ImportConflict> _selectedConflicts = [];
    private Dictionary<DateTime, List<ImportConflict>> _conflictsByDay = [];

    protected override void OnInitialized()
    {
        _isLoading = true;
        try
        {
            base.OnParametersSet();

            _conflictsByDay.Clear();
            _selectedDay = null;
            _selectedConflicts = [];

            _conflictsByDay = Conflicts
                .Where(c => c.ImportEntry is not null || c.ExistingEntry is not null)
                .GroupBy(c => (c.ImportEntry?.PostingDate ?? c.ExistingEntry!.PostingDate).Date)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            if (SkipExactMatches)
            {
                var keysToRemove = _conflictsByDay.Where(x => x.Value.All(y => y.IsExactMatch))
                                        .Select(x => x.Key)
                                        .ToList();

                foreach (var key in keysToRemove)
                    _conflictsByDay.Remove(key);
            }

            if (_conflictsByDay.Count != 0)
            {
                _selectedDay = _conflictsByDay.Keys.OrderBy(k => k).First();
                _selectedConflicts = _selectedDay.HasValue ? _conflictsByDay[_selectedDay.Value] : [];
                AccountId = Conflicts.First().AccountId;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing BankEntryConflictResolver for account {AccountId}", AccountId);
        }
        _isLoading = false;
    }

    private async Task OnPickImported()
    {
        _isLoading = true;
        try
        {
            var resolvedImports = _selectedConflicts.Select(c => new ResolvedImportConflict(c.AccountId, true, c.ImportEntry, false, c.ExistingEntry?.EntryId))
                                        .ToList();

            await BankAccountImportHttpClient.ResolveImportConflictsAsync(resolvedImports);
            RemoveSelectedDayAndAdvance();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error resolving import conflicts for account {AccountId}", AccountId);
        }
        _isLoading = false;
    }

    private void OnPickExisting()
    {
        _isLoading = true;
        try
        {
            RemoveSelectedDayAndAdvance();
        }
        catch (Exception ex)
        {
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
}