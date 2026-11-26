using FinanceManager.Contracts.FinancialAccounts.Shared;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;

namespace FinanceManager.Tests.Integration.Support.LegacyAccountClients;

internal static class FinancialLabelMapping
{
    public static FinancialLabel ToDomain(FinancialLabelDto label) => new()
    {
        Id = label.Id,
        Name = label.Name,
        Classifications = label.Classifications?.Select(item => new FinancialLabelClassification
        {
            Id = item.Id,
            LabelId = item.LabelId,
            Kind = item.Kind,
            Value = item.Value
        }).ToList()!
    };
}