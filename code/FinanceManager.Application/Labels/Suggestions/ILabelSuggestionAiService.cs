using FinanceManager.Domain.Entities.Shared.Accounts;

namespace FinanceManager.Application.Labels.Suggestions;

public interface ILabelSuggestionAiService
{
    Task<IReadOnlyList<LabelSuggestion>> SuggestLabels(
        int entrySampleSize = 100,
        int maxSuggestions = 5,
        CancellationToken cancellationToken = default);
}