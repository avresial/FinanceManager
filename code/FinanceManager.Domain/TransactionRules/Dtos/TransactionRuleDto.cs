using FinanceManager.Domain.TransactionRules.Entities;
using System.Text.Json;

namespace FinanceManager.Domain.TransactionRules.Dtos;

public sealed record TransactionRuleDto(
    Guid Id,
    string Name,
    int Order,
    bool IsEnabled,
    bool StopProcessing,
    IReadOnlyList<TransactionRuleConditionDto> Conditions,
    IReadOnlyList<TransactionRuleActionDto> Actions,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static TransactionRuleDto FromEntity(TransactionRuleDefinition entity)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return new(
            entity.Id,
            entity.Name,
            entity.Order,
            entity.IsEnabled,
            entity.StopProcessing,
            JsonSerializer.Deserialize<List<TransactionRuleConditionDto>>(entity.ConditionsJson, options) ?? [],
            JsonSerializer.Deserialize<List<TransactionRuleActionDto>>(entity.ActionsJson, options) ?? [],
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}