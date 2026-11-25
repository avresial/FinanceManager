using FinanceManager.Domain.Transactions.Investments.Dtos;

namespace FinanceManager.Domain.Portfolio.Valuation.Dtos;

/// <summary>An owner's pooled holding of one listing together with its value in the requested currency. An oversold holding has a negative value.</summary>
public sealed record InvestmentHoldingValue(InvestmentHolding Holding, decimal Value);