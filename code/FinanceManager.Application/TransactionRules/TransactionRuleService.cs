using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Domain.TransactionRules.Models;
using FinanceManager.Domain.TransactionRules.Repositories;
using FinanceManager.Domain.TransactionRules.Services;

namespace FinanceManager.Application.TransactionRules;

public sealed class TransactionRuleService(
    ITransactionRuleRepository repository,
    ITransactionRuleEngineService engineService,
    IFinancialLabelsRepository labelsRepository,
    ICurrencyAccountRepository<CurrencyAccount> accountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> entryRepository,
    IFinancialAlertService financialAlertService) : ITransactionRuleService
{
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
        TransactionRuleCommandValidator.Validate(command);
        var definition = command.ToDefinition(userId);
        await repository.Add(definition, cancellationToken);
        return TransactionRuleDto.FromEntity(definition);
    }

    public async Task<TransactionRuleDto?> UpdateRuleAsync(int userId, Guid id, UpdateTransactionRule command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        TransactionRuleCommandValidator.Validate(command);
        var definition = await repository.GetById(userId, id, cancellationToken);
        if (definition is null) return null;

        command.ApplyTo(definition);
        await repository.Update(definition, cancellationToken);
        return TransactionRuleDto.FromEntity(definition);
    }

    public async Task<IReadOnlyList<TransactionRuleTestResultDto>> TestAsync(
        int userId,
        CreateTransactionRule command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        TransactionRuleCommandValidator.Validate(command);

        var rule = new TransactionRule(
            Guid.Empty,
            command.Name.Trim(),
            1,
            TransactionRuleRuntimeBuilder.BuildConditions(command.Conditions),
            TransactionRuleRuntimeBuilder.BuildActions(command.Actions));
        var accounts = await accountRepository.GetAll(userId);
        var constrainedAccountIds = command.Conditions
            .Where(condition => condition.Type.Equals("Account", StringComparison.OrdinalIgnoreCase))
            .SelectMany(condition => condition.AccountIds)
            .ToHashSet();
        if (constrainedAccountIds.Count > 0)
            accounts = accounts.Where(account => constrainedAccountIds.Contains(account.AccountId)).ToList();
        var availableLabels = await LoadLabelsAsync(cancellationToken);
        var results = new List<TransactionRuleTestResultDto>(5);

        foreach (var account in accounts)
        {
            var months = (await entryRepository.GetPostingDates(account.AccountId))
                .Select(date => new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind))
                .Distinct()
                .OrderDescending();

            foreach (var monthStart in months)
            {
                var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                await foreach (var entry in entryRepository.Get(account.AccountId, monthStart, monthEnd, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var before = ToFacts(entry);
                    if (!rule.Matches(before))
                        continue;

                    var sandboxEntry = entry.GetCopy();
                    sandboxEntry.Labels = entry.Labels.ToList();
                    _ = ApplyToEntry(sandboxEntry, [rule], availableLabels);
                    var after = ToFacts(sandboxEntry);
                    results.Add(new(
                        entry.AccountId,
                        account.Name,
                        entry.EntryId,
                        entry.PostingDate,
                        entry.ValueChange,
                        before,
                        after,
                        before != after));

                    if (results.Count == 5)
                        return results;
                }
            }
        }

        return results;
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
        var runtimeRules = rules.Select(TransactionRuleRuntimeBuilder.FromDefinition).ToList();
        var transactionFacts = new TransactionFacts(facts.Contractor ?? string.Empty, facts.Description ?? string.Empty,
            facts.AccountId, facts.Amount, facts.Direction, facts.Labels ?? []);
        return engineService.RunRules(runtimeRules, transactionFacts);
    }

    public async Task<bool> ApplyToEntryAsync(int userId, CurrencyAccountEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var application = await LoadApplicationAsync(userId, cancellationToken);
        return application.ApplyTo(entry);
    }

    public async Task<TransactionRuleApplication> LoadApplicationAsync(int userId, CancellationToken cancellationToken = default)
    {
        var definitions = await repository.GetByUserId(userId, cancellationToken);
        var runtimeRules = definitions.Select(TransactionRuleRuntimeBuilder.FromDefinition).ToList();
        var labels = await LoadLabelsAsync(cancellationToken);
        return new(runtimeRules, labels);
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

        var application = await LoadApplicationAsync(userId, cancellationToken);
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
                    if (application.ApplyTo(entry) && await entryRepository.Update(entry))
                        updated++;
                }

                continue;
            }

            await foreach (var entry in entryRepository.Get(account.AccountId, DateTime.UnixEpoch, DateTime.MaxValue, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                examined++;
                if (application.ApplyTo(entry) && await entryRepository.Update(entry))
                    updated++;
            }
        }

        if (updated > 0)
            await financialAlertService.EvaluateAlertsAsync(userId, cancellationToken);

        return new(examined, updated);
    }

    private async Task<List<FinancialLabel>> LoadLabelsAsync(CancellationToken cancellationToken)
    {
        var labels = new List<FinancialLabel>();
        await foreach (var label in labelsRepository.GetLabels(cancellationToken).WithCancellation(cancellationToken))
            labels.Add(label);
        return labels;
    }

    internal static bool ApplyToEntry(CurrencyAccountEntry entry, IReadOnlyCollection<TransactionRule> rules, IReadOnlyCollection<FinancialLabel> availableLabels)
    {
        var facts = ToFacts(entry);
        var result = TransactionRuleEngine.Run(rules, facts);
        if (!result.HasChanges) return false;

        var existingByName = entry.Labels
            .GroupBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var availableByName = availableLabels
            .GroupBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var resolvedLabels = result.FinalFacts.Labels
            .Where(name => existingByName.ContainsKey(name) || availableByName.ContainsKey(name))
            .Select(name => existingByName.GetValueOrDefault(name) ?? availableByName[name])
            .DistinctBy(label => label.Id)
            .ToList();
        var finalContractor = string.IsNullOrWhiteSpace(result.FinalFacts.Contractor) ? null : result.FinalFacts.Contractor;
        var contractorChanged = !string.Equals(entry.ContractorDetails, finalContractor, StringComparison.Ordinal);
        var descriptionChanged = !string.Equals(entry.Description, result.FinalFacts.Description, StringComparison.Ordinal);
        var labelsChanged = !entry.Labels.Select(label => label.Id).SequenceEqual(resolvedLabels.Select(label => label.Id));
        if (!contractorChanged && !descriptionChanged && !labelsChanged)
            return false;

        entry.ContractorDetails = finalContractor;
        entry.Description = result.FinalFacts.Description;
        entry.Labels = resolvedLabels;
        return true;
    }

    private static TransactionFacts ToFacts(CurrencyAccountEntry entry) =>
        new(entry.ContractorDetails ?? string.Empty, entry.Description, entry.AccountId,
            Math.Abs(entry.ValueChange), entry.ValueChange switch
            {
                > 0 => TransactionDirection.Income,
                < 0 => TransactionDirection.Expense,
                _ => TransactionDirection.Transfer
            }, entry.Labels.Select(label => label.Name).ToList());
}