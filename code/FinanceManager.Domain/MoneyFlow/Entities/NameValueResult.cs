namespace FinanceManager.Domain.MoneyFlow.Entities;

public class NameValueResult
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }


    public NameValueResult()
    {

    }

    public NameValueResult(string name, decimal value)
    {
        Name = name;
        Value = value;
    }
}