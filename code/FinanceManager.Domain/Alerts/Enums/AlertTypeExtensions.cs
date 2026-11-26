namespace FinanceManager.Domain.Alerts.Enums;

public static class AlertTypeExtensions
{
    /// <summary>Cash alerts compare native amounts in one currency, so they need a currency; subscription alerts do not.</summary>
    public static bool RequiresCurrency(this AlertType alertType) =>
        alertType is AlertType.AccountBalance or AlertType.CategorySpending or AlertType.MerchantSpending or AlertType.LargeTransaction;
}