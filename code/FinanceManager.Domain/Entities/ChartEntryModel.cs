namespace FinanceManager.Domain.Entities;

public class ChartEntryModel
{
    public DateTime Date { get; set; }
    public decimal Value { get; set; }

    public ChartEntryModel()
    {

    }

    public ChartEntryModel(DateTime date, decimal value)
    {
        Date = date;
        Value = value;
    }
}
