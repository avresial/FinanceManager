namespace FinanceManager.Core.Entities.MoneyFlowModels
{
    public class TimeSeriesModel
    {
        public string Name { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public decimal Value { get; set; }
    }
}
