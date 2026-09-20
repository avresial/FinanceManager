using FinanceManager.Application.TransactionRules;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;

namespace FinanceManager.Application.TransactionRules.Services;

public interface ITransactionRuleService
{
    Task<IReadOnlyList<TransactionRuleDto>> GetRulesAsync(int userId, CancellationToken cancellationToken = default);
    Task<TransactionRuleDto?> GetRuleAsync(int userId, Guid id, CancellationToken cancellationToken = default);
    Task<TransactionRuleDto> CreateRuleAsync(int userId, CreateTransactionRule command, CancellationToken cancellationToken = default);
    Task<TransactionRuleDto?> UpdateRuleAsync(int userId, Guid id, UpdateTransactionRule command, CancellationToken cancellationToken = default);
    Task<bool> SetEnabledAsync(int userId, Guid id, bool enabled, CancellationToken cancellationToken = default);
    Task<bool> DeleteRuleAsync(int userId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionRuleDto>?> ReorderAsync(int userId, ReorderTransactionRules command, CancellationToken cancellationToken = default);
    Task<FinanceManager.Domain.TransactionRules.TransactionRuleEngineResult> PreviewAsync(int userId, TransactionRulePreviewFacts facts, CancellationToken cancellationToken = default);
    Task<TransactionRuleApplication> LoadApplicationAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> ApplyToEntryAsync(int userId, CurrencyAccountEntry entry, CancellationToken cancellationToken = default);
    Task<TransactionRuleApplyResultDto> ApplyRetroactivelyAsync(int userId, ApplyTransactionRules command, CancellationToken cancellationToken = default);
}

/// <summary>Preview facts are kept separate from command records so the endpoint has a stable JSON shape.</summary>
public sealed record TransactionRulePreviewFacts(
    string Contractor,
    string Description,
    int AccountId,
    decimal Amount,
    FinanceManager.Domain.TransactionRules.Models.TransactionDirection Direction,
    IReadOnlyList<string> Labels);