namespace FinanceManager.Domain.Entities;

public class Currency
{
    public int Id { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;

    public Currency()
    {

    }
    public Currency(int id, string shortName, string symbol)
    {
        Id = id;
        ShortName = shortName;
        Symbol = symbol;
    }

    public override string ToString() => ShortName.ToUpper();
}
