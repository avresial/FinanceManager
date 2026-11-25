namespace FinanceManager.Components.Features.Transactions.Shared.Services;

public class TransactionDataSynchronizationService
{
    public event Action? TransactionsChanged;
    public async Task AccountChanged()
    {
        TransactionsChanged?.Invoke();
        await Task.CompletedTask;
    }
}