using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Actions;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Conditions;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Domain.TransactionRules.Models;
using FinanceManager.Domain.TransactionRules.Repositories;
using FinanceManager.Domain.TransactionRules.Services;
using System.Text.Json;

namespace FinanceManager.Application.TransactionRules;

public sealed class TransactionRuleService(
    ITransactionRuleRepository repository,
    ITransactionRuleEngineService engineService,
    IFinancialLabelsRepository labelsRepository,
    ICurrencyAccountRepository<CurrencyAccount> accountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> entryRepository,
    IFinancialAlertService financialAlertService) : ITransactionRuleService
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<TransactionRuleDto>> GetRulesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rules = await repository.GetByUserId(userId, cancellationToken);
        return rules.Select(TransactionRuleDto.FromEntity).ToList();
    }

    public async Task<TransactionRuleDto?> GetRuleAsync(int userId, Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await repository.GetById(userId, id, cancellationToken);
        return rule is null ? null : TransactionRuleDto.FromEntity(rule);
    }

    public async Task<TransactionRuleDto> CreateRuleAsync(int userId, CreateTransactionRule command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command.Name, command.Conditions, command.Actions);
        var existing = await repository.GetByUserId(userId, cancellationToken);
        var definition = CreateDefinition(userId, command, existing.Count == 0 ? 1 : existing.Max(x => x.Order) + 1);
        await repository.Add(definition, cancellationToken);
        return TransactionRuleDto.FromEntity(definition);
    }

    public async Task<TransactionRuleDto?> UpdateRuleAsync(int userId, Guid id, UpdateTransactionRule command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command.Name, command.Conditions, command.Actions);
        var definition = await repository.GetById(userId, id, cancellationToken);
        if (definition is null) return null;

        ApplyDefinition(definition, command);
        await repository.Update(definition, cancellationToken);
        return TransactionRuleDto.FromEntity(definition);
    }

    public async Task<bool> SetEnabledAsync(int userId, Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var definition = await repository.GetById(userId, id, cancellationToken);
        if (definition is null) return false;

        definition.IsEnabled = enabled;
        definition.UpdatedAt = DateTime.UtcNow;
        await repository.Update(definition, cancellationToken);
        return true;
    }

    public Task<bool> DeleteRuleAsync(int userId, Guid id, CancellationToken cancellationToken = default) =>
        repository.Delete(userId, id, cancellationToken);

    public async Task<IReadOnlyList<TransactionRuleDto>?> ReorderAsync(int userId, ReorderTransactionRules command, CancellationToken cancellationToken = default)
    {
        if (command is null || command.RuleIds is null)
            throw new ArgumentException("Rule ids are required.", nameof(command));

        var rules = await repository.GetByUserId(userId, cancellationToken);
        if (command.RuleIds.Count != rules.Count || command.RuleIds.Distinct().Count() != command.RuleIds.Count ||
            command.RuleIds.Any(id => rules.All(rule => rule.Id != id)))
            throw new ArgumentException("The reorder request must contain every rule exactly once.", nameof(command));

        if (!await repository.Reorder(userId, command.RuleIds, cancellationToken))
            return null;

        return (await repository.GetByUserId(userId, cancellationToken)).Select(TransactionRuleDto.FromEntity).ToList();
    }

    public async Task<TransactionRuleEngineResult> PreviewAsync(int userId, TransactionRulePreviewFacts facts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var rules = await repository.GetByUserId(userId, cancellationToken);
        var runtimeRules = rules.Select(ToRuntimeRule).ToList();
        var transactionFacts = new TransactionFacts(facts.Contractor ?? string.Empty, facts.Description ?? string.Empty,
            facts.AccountId, facts.Amount, facts.Direction, facts.Labels ?? []);
        return engineService.RunRules(runtimeRules, transactionFacts);
    }

    public async Task<bool> ApplyToEntryAsync(int userId, CurrencyAccountEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var definitions = await repository.GetByUserId(userId, cancellationToken);
        var runtimeRules = definitions.Select(ToRuntimeRule).ToList();
        var labels = await LoadLabelsAsync(cancellationToken);
        return ApplyToEntry(entry, runtimeRules, labels);
    }

    public async Task<TransactionRuleApplyResultDto> ApplyRetroactivelyAsync(int userId, ApplyTransactionRules command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Confirmed)
            throw new ArgumentException("Retroactive application requires explicit confirmation.", nameof(command));
        if (command.EntryId is not null && command.AccountId is null)
            throw new ArgumentException("AccountId is required when EntryId is supplied.", nameof(command));

        var accounts = await accountRepository.GetAll(userId);
        if (command.AccountId is int requestedAccountId)
            accounts = accounts.Where(account => account.AccountId == requestedAccountId).ToList();

        var definitions = await repository.GetByUserId(userId, cancellationToken);
        var runtimeRules = definitions.Select(ToRuntimeRule).ToList();
        var labels = await LoadLabelsAsync(cancellationToken);
        var examined = 0;
        var updated = 0;

        foreach (var account in accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (command.EntryId is int requestedEntryId)
            {
                var entry = await entryRepository.Get(account.AccountId, requestedEntryId);
                if (entry is not null)
                {
                    examined++;
                    if (ApplyToEntry(entry, runtimeRules, labels) && await entryRepository.Update(entry))
                        updated++;
                }

                continue;
            }

            await foreach (var entry in entryRepository.Get(account.AccountId, DateTime.UnixEpoch, DateTime.MaxValue, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                examined++;
                if (ApplyToEntry(entry, runtimeRules, labels) && await entryRepository.Update(entry))
                    updated++;
            }
        }

        if (updated > 0)
            await financialAlertService.EvaluateAlertsAsync(userId, cancellationToken);

        return new(examined, updated);
    }

    private static TransactionRuleDefinition CreateDefinition(int userId, CreateTransactionRule command, int order) =>
        new()
        {
            UserId = userId,
            Name = command.Name.Trim(),
            Order = order,
            IsEnabled = command.IsEnabled,
            StopProcessing = command.StopProcessing,
            ConditionsJson = JsonSerializer.Serialize(command.Conditions, _jsonOptions),
            ActionsJson = JsonSerializer.Serialize(command.Actions, _jsonOptions)
        };

    private static void ApplyDefinition(TransactionRuleDefinition definition, UpdateTransactionRule command)
    {
        definition.Name = command.Name.Trim();
        definition.IsEnabled = command.IsEnabled;
        definition.StopProcessing = command.StopProcessing;
        definition.ConditionsJson = JsonSerializer.Serialize(command.Conditions, _jsonOptions);
        definition.ActionsJson = JsonSerializer.Serialize(command.Actions, _jsonOptions);
        definition.UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateCommand(string? name, IReadOnlyList<TransactionRuleConditionDto>? conditions, IReadOnlyList<TransactionRuleActionDto>? actions)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Rule name is required and must be at most 200 characters.", nameof(name));
        if (conditions is null || actions is null)
            throw new ArgumentException("Conditions and actions are required.");

        // Constructing the runtime rule validates enum values, patterns, and required fields.
        _ = BuildConditions(conditions);
        _ = BuildActions(actions);
    }

    private static TransactionRule ToRuntimeRule(TransactionRuleDefinition definition)
    {
        var conditions = JsonSerializer.Deserialize<List<TransactionRuleConditionDto>>(definition.ConditionsJson, _jsonOptions) ?? [];
        var actions = JsonSerializer.Deserialize<List<TransactionRuleActionDto>>(definition.ActionsJson, _jsonOptions) ?? [];
        return new TransactionRule(definition.Id, definition.Name, definition.Order, BuildConditions(conditions), BuildActions(actions),
            definition.StopProcessing, definition.IsEnabled);
    }

    private static IReadOnlyCollection<FinanceManager.Domain.TransactionRules.Conditions.ITransactionRuleCondition> BuildConditions(IEnumerable<TransactionRuleConditionDto> source)
    {
        var result = new List<FinanceManager.Domain.TransactionRules.Conditions.ITransactionRuleCondition>();
        foreach (var dto in source)
        {
            var type = dto.Type?.Trim() ?? string.Empty;
            switch (type.ToLowerInvariant())
            {
                case "contractor":
                    result.Add(new ContractorCondition(dto.Pattern ?? throw new ArgumentException("Contractor pattern is required."), dto.MatchOperator, dto.IgnoreCase));
                    break;
                case "description":
                    result.Add(new DescriptionCondition(dto.Pattern ?? throw new ArgumentException("Description pattern is required."), dto.MatchOperator, dto.IgnoreCase));
                    break;
                case "account":
                    if (dto.AccountIds is null || dto.AccountIds.Count == 0)
                        throw new ArgumentException("At least one account id is required.");
                    result.Add(new AccountCondition(dto.AccountIds));
                    break;
                case "direction":
                    if (!Enum.IsDefined(dto.Direction)) throw new ArgumentException("Direction is invalid.");
                    result.Add(new DirectionCondition(dto.Direction));
                    break;
                case "amount":
                    if (!Enum.IsDefined(dto.Direction)) throw new ArgumentException("Direction is invalid.");
                    if (dto.MinAmount is < 0m || dto.MaxAmount is < 0m || dto.Threshold is < 0m)
                        throw new ArgumentException("Amount thresholds must be non-negative.");
                    if (dto.MinAmount is decimal lower && dto.MaxAmount is decimal upper && lower > upper)
                        throw new ArgumentException("Minimum amount cannot exceed maximum amount.");
                    if ((dto.MinAmount is not null || dto.MaxAmount is not null) && dto.Threshold is not null)
                        throw new ArgumentException("Use either an amount threshold or an amount range, not both.");
                    if (dto.MinAmount is decimal min)
                        result.Add(new AmountCondition(dto.Direction, min, AmountComparison.GreaterThanOrEqual));
                    if (dto.MaxAmount is decimal max)
                        result.Add(new AmountCondition(dto.Direction, max, AmountComparison.LessThanOrEqual));
                    if (dto.Threshold is decimal threshold)
                        result.Add(new AmountCondition(dto.Direction, threshold, dto.Comparison));
                    if (dto.MinAmount is null && dto.MaxAmount is null && dto.Threshold is null)
                        throw new ArgumentException("An amount threshold or range is required.");
                    break;
                default:
                    throw new ArgumentException($"Unknown transaction rule condition type '{dto.Type}'.");
            }
        }

        return result;
    }

    private static IReadOnlyCollection<ITransactionRuleAction> BuildActions(IEnumerable<TransactionRuleActionDto> source)
    {
        var result = new List<ITransactionRuleAction>();
        foreach (var dto in source)
        {
            var type = dto.Type?.Trim() ?? string.Empty;
            switch (type.ToLowerInvariant())
            {
                case "setlabels":
                case "labels":
                    result.Add(new SetLabelsAction(dto.Labels ?? [], dto.ReplaceExisting));
                    break;
                case "normalizecontractor":
                case "contractor":
                    result.Add(new NormalizeContractorAction(dto.Value ?? throw new ArgumentException("Contractor normalization value is required.")));
                    break;
                case "normalizedescription":
                case "description":
                    result.Add(new NormalizeDescriptionAction(dto.Value ?? throw new ArgumentException("Description normalization value is required.")));
                    break;
                default:
                    throw new ArgumentException($"Unknown transaction rule action type '{dto.Type}'.");
            }
        }

        return result;
    }

    private async Task<List<FinancialLabel>> LoadLabelsAsync(CancellationToken cancellationToken)
    {
        var labels = new List<FinancialLabel>();
        await foreach (var label in labelsRepository.GetLabels(cancellationToken).WithCancellation(cancellationToken))
            labels.Add(label);
        return labels;
    }

    private static bool ApplyToEntry(CurrencyAccountEntry entry, IReadOnlyCollection<TransactionRule> rules, IReadOnlyCollection<FinancialLabel> availableLabels)
    {
        var facts = new TransactionFacts(entry.ContractorDetails ?? string.Empty, entry.Description, entry.AccountId,
            Math.Abs(entry.ValueChange), entry.ValueChange switch
            {
                > 0 => TransactionDirection.Income,
                < 0 => TransactionDirection.Expense,
                _ => TransactionDirection.Transfer
            }, entry.Labels.Select(label => label.Name).ToList());
        var result = TransactionRuleEngine.Run(rules, facts);
        if (!result.HasChanges) return false;

        entry.ContractorDetails = string.IsNullOrWhiteSpace(result.FinalFacts.Contractor) ? null : result.FinalFacts.Contractor;
        entry.Description = result.FinalFacts.Description;

        var existingByName = entry.Labels
            .GroupBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var availableByName = availableLabels
            .GroupBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        entry.Labels = result.FinalFacts.Labels
            .Where(name => existingByName.ContainsKey(name) || availableByName.ContainsKey(name))
            .Select(name => existingByName.GetValueOrDefault(name) ?? availableByName[name])
            .DistinctBy(label => label.Id)
            .ToList();
        return true;
    }
}