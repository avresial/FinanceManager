namespace FinanceManager.Components.Services;

public class AccountDataSynchronizationService
{
    public event Action? AccountsChanged;
    public async Task AccountChanged()
    {
        AccountsChanged?.Invoke();
        await Task.CompletedTask;
    }
}
