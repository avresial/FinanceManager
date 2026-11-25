using FinanceManager.Contracts.Labels;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Components.Features.Labels.HttpClients;

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