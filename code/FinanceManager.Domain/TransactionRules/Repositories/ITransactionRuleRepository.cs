using FinanceManager.Domain.TransactionRules.Entities;

namespace FinanceManager.Domain.TransactionRules.Repositories;

public interface ITransactionRuleRepository
{
    Task<IReadOnlyList<TransactionRuleDefinition>> GetByUserId(int userId, CancellationToken cancellationToken = default);
    Task<TransactionRuleDefinition?> GetById(int userId, Guid id, CancellationToken cancellationToken = default);
    Task<TransactionRuleDefinition> Add(TransactionRuleDefinition rule, CancellationToken cancellationToken = default);
    Task Update(TransactionRuleDefinition rule, CancellationToken cancellationToken = default);
    Task<bool> Delete(int userId, Guid id, CancellationToken cancellationToken = default);
    Task<bool> Reorder(int userId, IReadOnlyList<Guid> orderedIds, CancellationToken cancellationToken = default);
}