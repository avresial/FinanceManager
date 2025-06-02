using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Commands.Account
{
    public record UpdateAccount(int accountId, string accountName, AccountLabel? accountType = null);
}
