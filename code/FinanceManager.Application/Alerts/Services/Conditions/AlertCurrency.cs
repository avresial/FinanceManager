using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Entities;

namespace FinanceManager.Application.Alerts.Services.Conditions;

internal static class AlertCurrency
{
    /// <summary>The currency a cash alert compares amounts in. An alert without one is reported as an evaluation error.</summary>
    public static int Require(FinancialAlert alert) =>
        alert.CurrencyId ?? throw new InvalidOperationException("Alert requires a currency.");

    public static string CodeOf(AlertEvaluationSnapshot snapshot, int currencyId) =>
        snapshot.CurrencyCodes.GetValueOrDefault(currencyId) ?? $"Currency #{currencyId}";
}