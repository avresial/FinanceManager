using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Commands.Account
{
    public record UpdateAccount(int accountId, string accountName, AccountType? accountType = null);
}
