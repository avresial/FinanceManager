using FinanceManager.Application.MoneyFlow.Spending;
using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Labels.Seeders;

public class FinancialLabelSeeder(IFinancialLabelsRepository financialLabelsRepository, ILogger<FinancialLabelSeeder> logger) : ISeeder
{
    private readonly IReadOnlyCollection<string> _defaultLabels =
    [
        "Undisclosed Expense",
        "Undisclosed Income",
        "Salary",
        "Groceries",
        "Rent",
        "Utilities",
        "Entertainment",
        "Subscription",
        "Transportation",
        "Healthcare",
        "Education",
        "Dining Out",
        "Travel",
        "Investment",
        "Loan",
        WellKnownFinancialLabels.NoMatch,
    ];

    // Default SpendingNecessity classification per label. Without one, CurrencyExpenseDistributionService
    // silently drops every expense carrying the label, leaving the expense-distribution chart empty
    // (notably on the guest demo, whose per-session DB never runs the AI label setter). Income-only labels
    // (Salary, Undisclosed Income) and the NoMatch sentinel are intentionally left unclassified.
    private static readonly IReadOnlyDictionary<string, string> _spendingNecessityByLabel =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Groceries"] = FinancialLabelClassificationCatalog.EssentialValue,
            ["Rent"] = FinancialLabelClassificationCatalog.EssentialValue,
            ["Utilities"] = FinancialLabelClassificationCatalog.EssentialValue,
            ["Transportation"] = FinancialLabelClassificationCatalog.EssentialValue,
            ["Healthcare"] = FinancialLabelClassificationCatalog.EssentialValue,
            ["Education"] = FinancialLabelClassificationCatalog.EssentialValue,
            ["Loan"] = FinancialLabelClassificationCatalog.EssentialValue,
            ["Entertainment"] = FinancialLabelClassificationCatalog.WantValue,
            ["Subscription"] = FinancialLabelClassificationCatalog.WantValue,
            ["Dining Out"] = FinancialLabelClassificationCatalog.WantValue,
            ["Travel"] = FinancialLabelClassificationCatalog.WantValue,
            ["Investment"] = FinancialLabelClassificationCatalog.InvestmentValue,
            ["Undisclosed Expense"] = FinancialLabelClassificationCatalog.UnknownValue,
        };

    public async Task Seed(CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await financialLabelsRepository
                .GetLabels(cancellationToken)
                .Select(l => l.Name)
                .ToListAsync(cancellationToken);

            var existingSet = new HashSet<string>(existing, StringComparer.Ordinal);

            // Full seed only for empty DBs; on populated DBs we still backfill the NoMatch sentinel.
            var labelsToAdd = existing.Count == 0
                ? _defaultLabels
                : _defaultLabels.Where(name => name == WellKnownFinancialLabels.NoMatch && !existingSet.Contains(name)).ToList();

            foreach (var label in labelsToAdd)
            {
                if (existingSet.Contains(label)) continue;

                if (await financialLabelsRepository.Add(label, cancellationToken))
                    logger.LogInformation("Seeded financial label: {Label}", label);
                else
                    logger.LogWarning("Failed to seed financial label: {Label}", label);
            }

            await SeedClassifications(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding financial labels");
        }
    }

    // Backfills the default SpendingNecessity classification onto any seeded label that lacks one. Runs on
    // populated DBs too, but never touches a label an admin has already classified.
    private async Task SeedClassifications(CancellationToken cancellationToken)
    {
        var labels = await financialLabelsRepository.GetLabels(cancellationToken).ToListAsync(cancellationToken);

        foreach (var label in labels)
        {
            if (!_spendingNecessityByLabel.TryGetValue(label.Name, out var value)) continue;
            if (label.Classifications.Any(c => c.Kind == FinancialLabelClassificationCatalog.SpendingNecessityKind)) continue;

            if (await financialLabelsRepository.AddClassification(label.Id, FinancialLabelClassificationCatalog.SpendingNecessityKind, value, cancellationToken))
                logger.LogInformation("Seeded {Kind}={Value} classification for label: {Label}", FinancialLabelClassificationCatalog.SpendingNecessityKind, value, label.Name);
        }
    }
}