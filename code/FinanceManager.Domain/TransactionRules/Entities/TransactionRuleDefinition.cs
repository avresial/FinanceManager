namespace FinanceManager.Domain.TransactionRules.Entities;

/// <summary>
/// Persisted representation of a transaction automation rule. The executable
/// condition and action objects are stored as JSON by the infrastructure layer so
/// the domain rule engine remains free of persistence concerns.
/// </summary>
public sealed class TransactionRuleDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool StopProcessing { get; set; }
    public string ConditionsJson { get; set; } = "[]";
    public string ActionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}