using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.MoneyFlow.Models;

public sealed record CashFlowForecastPageModel(CashFlowForecast Forecast, string Currency);