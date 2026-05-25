namespace FinanceManager.Domain.Entities.MoneyFlowModels;

public class TimeSeriesModel
{
    public string Name { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public decimal Value { get; set; }

    public TimeSeriesModel()
    {

    }
    public TimeSeriesModel(DateTime dateTime, decimal value, string? name = null)
    {
        Name = string.IsNullOrEmpty(name) ? string.Empty : name;
        DateTime = dateTime;
        Value = value;
    }
}