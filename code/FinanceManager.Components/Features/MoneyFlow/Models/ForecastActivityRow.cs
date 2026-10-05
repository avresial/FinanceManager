using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.MoneyFlow.Models;

/// <summary>One expected transaction with the projected balance right after it. The balance is null when no starting balance is known.</summary>
public sealed record ForecastActivityRow(CashFlowForecastTransaction Transaction, decimal? RunningBalance);