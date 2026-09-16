namespace FinanceManager.Domain.MoneyFlow.Entities;

/// <summary>A dated investor-perspective cash flow used by the XIRR calculator.</summary>
public sealed record XirrCashFlow(DateTime Date, decimal Amount);