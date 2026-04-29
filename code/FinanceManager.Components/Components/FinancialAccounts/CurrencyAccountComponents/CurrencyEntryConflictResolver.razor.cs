using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.Imports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.FinancialAccounts.CurrencyAccountComponents;

public partial class CurrencyEntryConflictResolver
{
    [Inject] public required CurrencyAccountImportHttpClient accountImportHttpClient { get; set; }
    [Inject] public required ILogger<CurrencyEntryConflictResolver> Logger { get; set; }

    [Parameter] public IReadOnlyCollection<ImportConflict> Conflicts { get; set; } = [];
    [Parameter] public IReadOnlyCollection<ImportJobConflict>? JobConflicts { get; set; }
    [Parameter] public Guid? JobId { get; set; }
    [Parameter] public bool SkipExactMatches { get; set; } = true;
    [Parameter] public required string AccountName { get; set; }
    [Parameter] public EventCallback<IReadOnlyCollection<string>> OnConflictsResolved { get; set; }

    private bool _isLoading = false;
    private int AccountId { get; set; }
    private DateTime? _selectedDay = null;
    private List<ResolverConflict> _selectedConflicts = [];
    private Dictionary<DateTime, List<ResolverConflict>> _conflictsByDay = [];

    protected override void OnParametersSet()
    {
        _isLoading = true;
        try
        {
            base.OnParametersSet();

            _conflictsByDay.Clear();
            _selectedDay = null;
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
                _selectedDay = _conflictsByDay.Keys.OrderBy(k => k).First();
                _selectedConflicts = _selectedDay.HasValue ? _conflictsByDay[_selectedDay.Value] : [];
                AccountId = _selectedConflicts.First().Conflict.AccountId;
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
        try
        {
            if (JobId.HasValue)
            {
                var pickedConflictIds = _selectedConflicts
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
                    await accountImportHttpClient.ResolveAsyncCurrencyImportConflictsAsync(request);

                if (pickedConflictIds.Count != 0)
                    await OnConflictsResolved.InvokeAsync(pickedConflictIds);
            }
            else
            {
                var resolvedImports = _selectedConflicts
                    .Select(c => new ResolvedImportConflict(c.Conflict.AccountId, true, c.Conflict.ImportEntry, false, c.Conflict.ExistingEntry?.EntryId))
                    .ToList();

                await accountImportHttpClient.ResolveImportConflictsAsync(resolvedImports);
            }

            RemoveSelectedDayAndAdvance();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error resolving import conflicts for account {AccountId}", AccountId);
        }
        _isLoading = false;
    }

    private async Task OnPickExisting()
    {
        _isLoading = true;
        try
        {
            if (JobId.HasValue)
            {
                var pickedConflictIds = _selectedConflicts
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
                    await accountImportHttpClient.ResolveAsyncCurrencyImportConflictsAsync(request);

                if (pickedConflictIds.Count != 0)
                    await OnConflictsResolved.InvokeAsync(pickedConflictIds);
            }

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

    private sealed record ResolverConflict(ImportConflict Conflict, string? ConflictId);
}