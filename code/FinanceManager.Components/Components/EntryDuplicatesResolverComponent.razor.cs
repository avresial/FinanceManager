using FinanceManager.Components.HttpContexts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components;
public partial class EntryDuplicatesResolverComponent
{
    private string _message = string.Empty;
    private bool _isScanning = false;
    private bool _isInitializing = false;

    private int _selectedPageCount = 1;
    private int _pageCount = 1;
    private int _elementsPerPage = 10;
    private List<(DuplicateEntry Duplicate, DateTime PostingDate, decimal ValueChange)> _duplicates { get; set; } = [];
    private List<(DuplicateEntry Duplicate, DateTime PostingDate, decimal ValueChange)> _displayedDuplicates { get; set; } = [];
    private Dictionary<int, Type>? financialAccounts = [];

    [Inject] public required ILogger<EntryDuplicatesResolverComponent> Logger { get; set; }
    [Inject] public required DuplicateEntryResolverService DuplicateEntryResolverService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required BankAccountHttpContext BankAccountHttpContext { get; set; }
    private void PageChanged(int i)
    {
        _selectedPageCount = i;
        _displayedDuplicates = _duplicates.Skip((i - 1) * _elementsPerPage).Take(_elementsPerPage).ToList();
    }

    protected override async Task OnInitializedAsync()
    {
        _isInitializing = true;
        try
        {
            financialAccounts = await FinancialAccountService.GetAvailableAccounts();
            await GetDuplicates();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task GetDuplicates()
    {
        _message = string.Empty;
        if (financialAccounts is null) return;
        int allDuplicatesCount = 0;

        foreach (var financialAccountId in financialAccounts.Keys)
        {
            var duplicatesCount = await DuplicateEntryResolverService.GetDuplicatesCount(financialAccountId);
            if (duplicatesCount == 0) continue;
            allDuplicatesCount += duplicatesCount;
        }

        foreach (var financialAccountId in financialAccounts.Keys)
        {
            var duplicatesCount = await DuplicateEntryResolverService.GetDuplicatesCount(financialAccountId);
            var duplicates = await DuplicateEntryResolverService.GetDuplicates(financialAccountId, 0, duplicatesCount);
            if (duplicates is null) continue;
            foreach (var duplicate in duplicates)
            {
                BankAccountEntry bankEntry = (await BankAccountHttpContext.GetEntry(duplicate.AccountId, duplicate.EntriesId.First()))!;
                _duplicates.Add((duplicate, bankEntry.PostingDate, bankEntry.ValueChange));
            }
        }

        _pageCount = (int)Math.Ceiling((double)_duplicates.Count / _elementsPerPage);
        _displayedDuplicates = _duplicates.Take(_elementsPerPage).ToList();

        if (_duplicates.Count == 0) _message = "No duplicates found";
    }
    private async Task Scan()
    {
        _isScanning = true;
        _duplicates.Clear();

        try
        {
            var loggedUser = await LoginService.GetLoggedUser();
            if (loggedUser is null)
            {
                Logger.LogWarning("User is not logged in. Cannot scan for duplicate entries.");
                return;
            }

            if (financialAccounts is null || financialAccounts.Count == 0)
            {
                Logger.LogWarning("No financial accounts to check");
                return;
            }

            foreach (var financialAccountId in financialAccounts.Keys) // maybe run this in parallel?
                await DuplicateEntryResolverService.Scan(financialAccountId);

            await GetDuplicates();


        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while scanning for duplicate entries.");
        }
        finally
        {
            _isScanning = false;
        }
    }

    private async Task ResolveDuplicates(int accountId, int duplicateId, int entryIdToBeRemained)
    {
        await DuplicateEntryResolverService.Resolve(accountId, duplicateId, entryIdToBeRemained);

        _duplicates.RemoveAll(d => d.Duplicate.Id == duplicateId && d.Duplicate.AccountId == accountId);

        _pageCount = (int)Math.Ceiling((double)_duplicates.Count / _elementsPerPage);

        if (_pageCount >= _selectedPageCount)
        {
            _displayedDuplicates = _duplicates.Skip((_selectedPageCount - 1) * _elementsPerPage).Take(_elementsPerPage).ToList();
        }
        else
        {
            _displayedDuplicates = _duplicates.Skip((_pageCount - 1) * _elementsPerPage).Take(_elementsPerPage).ToList();
        }
    }
}