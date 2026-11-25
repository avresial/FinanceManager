using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;
using FinanceManager.Domain.TransactionRules.Entities;
using FinanceManager.Domain.TransactionRules.Models;
using FinanceManager.Domain.TransactionRules.Repositories;
using FinanceManager.Domain.TransactionRules.Services;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FinanceManager.Application.TransactionRules;

public sealed class TransactionRuleService(
    ITransactionRuleRepository repository,
    ITransactionRuleEngineService engineService,
    IFinancialLabelsRepository labelsRepository,
    ICashTransactionReader cashTransactionReader,
    ITransactionEntryRepository<CashEntry> entryRepository,
    IFinancialAlertService financialAlertService,
    ILogger<TransactionRuleService> logger) : ITransactionRuleService
{
    private const int _historyPageSize = 200;
    private const int _maxTestResults = 5;

    // A rule that still carries a retired condition is reported once per process, not on every transaction write.
    private static readonly ConcurrentDictionary<Guid, byte> _reportedRetiredRules = new();

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
            TransactionRuleRuntimeBuilder.BuildActions(command.Actions),
            command.StopProcessing,
            command.IsEnabled);
        if (!rule.IsEnabled)
            return [];

        if (userId == 0)
            return [];

        var application = new TransactionRuleApplication([rule], await LoadLabelsAsync(cancellationToken));
        var results = new List<TransactionRuleTestResultDto>(_maxTestResults);

        // Newest first, one bounded page at a time, until enough matches are found or the history ends.
        OwnerHistoryBoundary? boundary = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await cashTransactionReader.GetHistory(userId,
                new OwnerHistoryQuery(null, null, _historyPageSize, boundary, IncludeDetails: true), cancellationToken);
            foreach (var entry in page)
            {
                var before = TransactionRuleApplication.ToFacts(entry);
                if (!rule.Matches(before))
                    continue;

                var sandboxEntry = TransactionRuleApplication.ToSandboxEntry(entry);
                _ = application.ApplyTo(sandboxEntry);
                var after = TransactionRuleApplication.ToFacts(sandboxEntry);
                results.Add(new(entry.EntryId, entry.PostingDate, entry.ValueChange, entry.Currency, before, after, before != after));

                if (results.Count == _maxTestResults)
                    return results;
            }

            if (page.Count < _historyPageSize)
                return results;

            var last = page[^1];
            boundary = new OwnerHistoryBoundary(last.PostingDate, last.EntryId, IncludeEqualId: false);
        }
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
        var runtimeRules = rules.Select(BuildRuntimeRule).ToList();
        var transactionFacts = new TransactionFacts(facts.Contractor ?? string.Empty, facts.Description ?? string.Empty,
            facts.Amount, facts.Direction, facts.Labels ?? []);
        return engineService.RunRules(runtimeRules, transactionFacts);
    }

    public async Task<bool> ApplyToEntryAsync(int userId, CashEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var application = await LoadApplicationAsync(userId, cancellationToken);
        return application.ApplyTo(entry);
    }

    public async Task<TransactionRuleApplication> LoadApplicationAsync(int userId, CancellationToken cancellationToken = default)
    {
        var definitions = await repository.GetByUserId(userId, cancellationToken);
        var runtimeRules = definitions.Select(BuildRuntimeRule).ToList();
        var labels = await LoadLabelsAsync(cancellationToken);
        return new(runtimeRules, labels);
    }

    public async Task<TransactionRuleApplyResultDto> ApplyRetroactivelyAsync(int userId, ApplyTransactionRules command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Confirmed)
            throw new ArgumentException("Retroactive application requires explicit confirmation.", nameof(command));
        if (userId == 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "A user is required.");
        if (command.EntryId is <= 0)
            throw new ArgumentException("EntryId must be positive.", nameof(command));

        var application = await LoadApplicationAsync(userId, cancellationToken);
        var examined = 0;
        var updated = 0;

        if (command.EntryId is long requestedEntryId)
        {
            if (await entryRepository.GetByOwner(userId, requestedEntryId, cancellationToken) is CashEntry entry)
            {
                examined++;
                if (application.ApplyTo(entry) && await entryRepository.UpdateByOwner(userId, entry, cancellationToken))
                    updated++;
            }
        }
        else
        {
            // Every owned cash entry, linked to an account or not, newest first in bounded keyset pages.
            OwnerHistoryBoundary? boundary = null;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await cashTransactionReader.GetHistory(userId,
                    new OwnerHistoryQuery(null, null, _historyPageSize, boundary, IncludeDetails: false), cancellationToken);
                foreach (var entry in await entryRepository.GetByIds(userId, [.. page.Select(row => row.EntryId)], cancellationToken))
                {
                    examined++;
                    if (application.ApplyTo(entry) && await entryRepository.UpdateByOwner(userId, entry, cancellationToken))
                        updated++;
                }

                if (page.Count < _historyPageSize)
                    break;

                var last = page[^1];
                boundary = new OwnerHistoryBoundary(last.PostingDate, last.EntryId, IncludeEqualId: false);
            }
        }

        if (updated > 0)
            await financialAlertService.EvaluateAlertsAsync(userId, cancellationToken);

        return new(examined, updated);
    }

    private TransactionRule BuildRuntimeRule(TransactionRuleDefinition definition)
    {
        var rule = TransactionRuleRuntimeBuilder.FromDefinition(definition, out var hasRetiredCondition);
        if (hasRetiredCondition && _reportedRetiredRules.TryAdd(definition.Id, 0))
            logger.LogWarning("Transaction rule {RuleId} still has a retired Account condition and is skipped. Edit the rule to re-enable it.", definition.Id);
        return rule;
    }

    private async Task<List<FinancialLabel>> LoadLabelsAsync(CancellationToken cancellationToken)
    {
        var labels = new List<FinancialLabel>();
        await foreach (var label in labelsRepository.GetLabels(cancellationToken).WithCancellation(cancellationToken))
            labels.Add(label);
        return labels;
    }

}