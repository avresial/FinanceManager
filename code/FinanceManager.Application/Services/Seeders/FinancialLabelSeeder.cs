using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

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
        WellKnownFinancialLabels.NoMatch,
    ];

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding financial labels");
        }
    }
}