namespace FinanceManager.Domain.MoneyFlow.Entities;

public class NameValueResult
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }

    // Set on the per-holding rows of the assets distribution ("Cash", "Bond", "Stock") so the UI can tell a ticker from a currency code.
    public string? Kind { get; set; }

    // The currency <see cref="Value"/> is expressed in when it is NOT converted to the display currency (cash balances and bond values).
    // Null means the value is already in the display currency.
    public string? CurrencyCode { get; set; }

    public NameValueResult()
    {
    }

    public NameValueResult(string name, decimal value)
    {
        Name = name;
        Value = value;
    }
}