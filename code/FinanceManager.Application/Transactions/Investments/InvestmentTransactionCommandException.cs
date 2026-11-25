namespace FinanceManager.Application.Transactions.Investments;

public sealed class InvestmentTransactionCommandException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}