namespace FinanceManager.Domain.MoneyFlow.Entities;

public class NameValueResult
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }

    // Set only on per-account rows so the UI can link to the account; names are not unique.
    public int? AccountId { get; set; }

    public NameValueResult()
    {

    }

    public NameValueResult(string name, decimal value)
    {
        Name = name;
        Value = value;
    }
}