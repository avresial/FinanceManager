using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Entities;
using System.Text.Json;

namespace FinanceManager.Application.TransactionRules;

internal static class TransactionRuleCommandExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public static TransactionRuleDefinition ToDefinition(this CreateTransactionRule command, int userId) =>
        new()
        {
            UserId = userId,
            Name = command.Name.Trim(),
            IsEnabled = command.IsEnabled,
            StopProcessing = command.StopProcessing,
            ConditionsJson = JsonSerializer.Serialize(command.Conditions, _jsonOptions),
            ActionsJson = JsonSerializer.Serialize(command.Actions, _jsonOptions)
        };

    public static void ApplyTo(this UpdateTransactionRule command, TransactionRuleDefinition definition)
    {
        definition.Name = command.Name.Trim();
        definition.IsEnabled = command.IsEnabled;
        definition.StopProcessing = command.StopProcessing;
        definition.ConditionsJson = JsonSerializer.Serialize(command.Conditions, _jsonOptions);
        definition.ActionsJson = JsonSerializer.Serialize(command.Actions, _jsonOptions);
        definition.UpdatedAt = DateTime.UtcNow;
    }
}