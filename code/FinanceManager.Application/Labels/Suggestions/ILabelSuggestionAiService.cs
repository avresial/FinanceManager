using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Labels.Entities;

namespace FinanceManager.Application.Labels.Suggestions;

public interface ILabelSuggestionAiService
{
    Task<IReadOnlyList<LabelSuggestion>> SuggestLabels(
        int entrySampleSize = 100,
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default);
}