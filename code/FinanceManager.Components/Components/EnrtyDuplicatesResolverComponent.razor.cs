using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components;
public partial class EnrtyDuplicatesResolverComponent
{
    [Inject] public required ILogger<EnrtyDuplicatesResolverComponent> Logger { get; set; }
    [Inject] public required DuplicateEntryResolverService DuplicateEntryResolverService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }

    private string _message = string.Empty;
    private bool _isScanning = false;

    private List<DuplicateEntry> _duplicates { get; set; } = [];
    private async Task Scan()
    {
        _isScanning = true;

        try
        {
            var loggedUser = await LoginService.GetLoggedUser();
            if (loggedUser is null)
            {
                Logger.LogWarning("User is not logged in. Cannot scan for duplicate entries.");
                return;
            }
            var financialAccounts = await FinancialAccountService.GetAvailableAccounts();
            if (financialAccounts is null || financialAccounts.Count == 0)
            {
                Logger.LogWarning("No financial accounts to check");
                return;
            }

            foreach (var financialAccountId in financialAccounts.Keys) // maybe run this in parallel?
            {
                await DuplicateEntryResolverService.Scan(financialAccountId);
                var duplicatesCount = await DuplicateEntryResolverService.GetDuplicatesCount(financialAccountId);
                if (duplicatesCount == 0) continue;

                var duplicates = await DuplicateEntryResolverService.GetDuplicates(financialAccountId, 0, duplicatesCount);
                if (duplicates is null) continue;

                _duplicates.AddRange(duplicates.ToList());
            }
            if (_duplicates.Count == 0)
            {
                _message = "No duplicates found";
            }
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
    }
}