using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Dtos;

/// <summary>Both asset projections from one read operation; never retained by the service.</summary>
public record AssetsDistributionResult(List<NameValueResult> PerType, List<NameValueResult> PerAccount);