namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>
/// One dated portfolio valuation period. <see cref="ExternalCashFlow"/> is positive when capital
/// enters the portfolio and negative when it leaves it.
/// </summary>
public sealed record TimeWeightedReturnPeriod(
    DateTime Date,
    decimal StartingValue,
    decimal ExternalCashFlow,
    decimal EndingValue);