namespace FinanceManager.Domain.Entities;

public class Currency
{
    public string ShortName { get; set; }
    public string Symbol { get; set; }

    public Currency()
    {

    }
    public Currency(string shortName, string symbol)
    {
        ShortName = shortName;
        Symbol = symbol;
    }
}
