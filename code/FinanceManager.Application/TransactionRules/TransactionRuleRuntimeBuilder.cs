using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Actions;
using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using System.Text.Json;

namespace FinanceManager.Application.TransactionRules;

internal static class TransactionRuleRuntimeBuilder
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static TransactionRule FromDefinition(TransactionRuleDefinition definition)
    {
        var conditions = JsonSerializer.Deserialize<List<TransactionRuleConditionDto>>(definition.ConditionsJson, _jsonOptions) ?? [];
        var actions = JsonSerializer.Deserialize<List<TransactionRuleActionDto>>(definition.ActionsJson, _jsonOptions) ?? [];
        return new TransactionRule(
            definition.Id,
            definition.Name,
            definition.Order,
            BuildConditions(conditions),
            BuildActions(actions),
            definition.StopProcessing,
            definition.IsEnabled);
    }

    public static void ValidateConditions(IEnumerable<TransactionRuleConditionDto> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var dto in source)
            _ = BuildConditionsForDto(dto);
    }

    public static void ValidateActions(IEnumerable<TransactionRuleActionDto> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var dto in source)
            _ = BuildAction(dto);
    }

    public static IReadOnlyCollection<ITransactionRuleCondition> BuildConditions(IEnumerable<TransactionRuleConditionDto> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new List<ITransactionRuleCondition>();
        foreach (var dto in source)
            result.AddRange(BuildConditionsForDto(dto));
        return result;
    }

    public static IReadOnlyCollection<ITransactionRuleAction> BuildActions(IEnumerable<TransactionRuleActionDto> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Select(BuildAction).ToList();
    }

    private static IReadOnlyCollection<ITransactionRuleCondition> BuildConditionsForDto(TransactionRuleConditionDto dto)
    {
        var type = dto.Type?.Trim() ?? string.Empty;
        return type.ToLowerInvariant() switch
        {
            "contractor" => [new ContractorCondition(
                dto.Pattern ?? throw new ArgumentException("Contractor pattern is required."),
                dto.MatchOperator,
                dto.IgnoreCase)],
            "description" => [new DescriptionCondition(
                dto.Pattern ?? throw new ArgumentException("Description pattern is required."),
                dto.MatchOperator,
                dto.IgnoreCase)],
            "account" => BuildAccountCondition(dto),
            "direction" => BuildDirectionCondition(dto),
            "amount" => BuildAmountConditions(dto),
            _ => throw new ArgumentException($"Unknown transaction rule condition type '{dto.Type}'.")
        };
    }

    private static IReadOnlyCollection<ITransactionRuleCondition> BuildAccountCondition(TransactionRuleConditionDto dto)
    {
        if (dto.AccountIds is null || dto.AccountIds.Count == 0)
            throw new ArgumentException("At least one account id is required.");
        return [new AccountCondition(dto.AccountIds)];
    }

    private static IReadOnlyCollection<ITransactionRuleCondition> BuildDirectionCondition(TransactionRuleConditionDto dto)
    {
        if (!Enum.IsDefined(dto.Direction))
            throw new ArgumentException("Direction is invalid.");
        return [new DirectionCondition(dto.Direction)];
    }

    private static IReadOnlyCollection<ITransactionRuleCondition> BuildAmountConditions(TransactionRuleConditionDto dto)
    {
        if (!Enum.IsDefined(dto.Direction))
            throw new ArgumentException("Direction is invalid.");
        if (dto.MinAmount is < 0m || dto.MaxAmount is < 0m || dto.Threshold is < 0m)
            throw new ArgumentException("Amount thresholds must be non-negative.");
        if (dto.MinAmount is decimal lower && dto.MaxAmount is decimal upper && lower > upper)
            throw new ArgumentException("Minimum amount cannot exceed maximum amount.");
        if ((dto.MinAmount is not null || dto.MaxAmount is not null) && dto.Threshold is not null)
            throw new ArgumentException("Use either an amount threshold or an amount range, not both.");

        var amountConditions = new List<ITransactionRuleCondition>();
        if (dto.MinAmount is decimal min)
            amountConditions.Add(new AmountCondition(dto.Direction, min, AmountComparison.GreaterThanOrEqual));
        if (dto.MaxAmount is decimal max)
            amountConditions.Add(new AmountCondition(dto.Direction, max, AmountComparison.LessThanOrEqual));
        if (dto.Threshold is decimal threshold)
            amountConditions.Add(new AmountCondition(dto.Direction, threshold, dto.Comparison));
        if (amountConditions.Count == 0)
            throw new ArgumentException("An amount threshold or range is required.");

        return amountConditions;
    }

    private static ITransactionRuleAction BuildAction(TransactionRuleActionDto dto)
    {
        var type = dto.Type?.Trim() ?? string.Empty;
        return type.ToLowerInvariant() switch
        {
            "setlabels" or "labels" => new SetLabelsAction(dto.Labels ?? [], dto.ReplaceExisting),
            "normalizecontractor" or "contractor" => new NormalizeContractorAction(
                dto.Value ?? throw new ArgumentException("Contractor normalization value is required.")),
            "normalizedescription" or "description" => new NormalizeDescriptionAction(
                dto.Value ?? throw new ArgumentException("Description normalization value is required.")),
            _ => throw new ArgumentException($"Unknown transaction rule action type '{dto.Type}'.")
        };
    }
}