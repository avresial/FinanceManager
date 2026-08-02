namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public sealed class InstrumentImportException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}