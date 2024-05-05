using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class AccountsService
    {
        public Dictionary<string, List<AccountEntry>> Accounts = new Dictionary<string, List<AccountEntry>>();
    }
}
