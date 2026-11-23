using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Shared;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FinanceManager.Api.Features.Transactions.Shared.Validation;

/// <summary>
/// Field rules the owner cash and bond requests share with the account-based commands. The request contracts stay free of
/// Domain, so the same rules are applied here at the API boundary.
/// </summary>
internal static class OwnerEntryRequestValidation
{
    private const int _maxTextLength = 512;

    public static void ValidateFields(ModelStateDictionary modelState, DateTime postingDate, string? description, bool descriptionRequired, string? contractorDetails = null)
    {
        var dateRule = new ReasonableDateAttribute();
        if (!dateRule.IsValid(postingDate))
            modelState.AddModelError(nameof(postingDate), dateRule.ErrorMessage!);
        if (descriptionRequired && string.IsNullOrWhiteSpace(description))
            modelState.AddModelError(nameof(description), "Description is required.");
        if (description is { Length: > _maxTextLength })
            modelState.AddModelError(nameof(description), $"Description must be at most {_maxTextLength} characters.");
        if (contractorDetails is { Length: > _maxTextLength })
            modelState.AddModelError(nameof(contractorDetails), $"Contractor details must be at most {_maxTextLength} characters.");
    }

    /// <summary>Resolves label ids in one pass over the label catalog. Returns null when an id is unknown.</summary>
    public static async Task<List<FinancialLabel>?> ResolveLabels(
        IFinancialLabelsRepository labelsRepository, IReadOnlyCollection<int>? labelIds, CancellationToken cancellationToken)
    {
        if (labelIds is null || labelIds.Count == 0) return [];

        var wanted = labelIds.ToHashSet();
        List<FinancialLabel> labels = [];
        await foreach (var label in labelsRepository.GetLabels(cancellationToken))
            if (wanted.Remove(label.Id))
                labels.Add(label);

        return wanted.Count == 0 ? labels : null;
    }
}