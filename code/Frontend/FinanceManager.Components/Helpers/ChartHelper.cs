namespace FinanceManager.Components.Helpers
{
    public static class ChartHelper
    {
        public static string GetCurrencyFormatter(string currency)
        {
            return @"function(value, opts) {
                    if (value === undefined) {return '';}
                    return Number(value).toLocaleString() + " + $" ' {currency}' " + ";}";
        }
    }
}
