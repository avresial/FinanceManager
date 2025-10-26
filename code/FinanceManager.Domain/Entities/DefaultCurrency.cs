namespace FinanceManager.Domain.Entities;
public static class DefaultCurrency
{
    public static Currency PLN = new("PLN", "zł"); // Default currency for the application, used in stock prices and financial transactions
}
