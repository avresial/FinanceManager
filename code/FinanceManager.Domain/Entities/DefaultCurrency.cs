namespace FinanceManager.Domain.Entities;
public static class DefaultCurrency
{
    public static Currency PLN => new(0, "PLN", "zł");
    public static Currency USD => new(1, "USD", "$");
}
