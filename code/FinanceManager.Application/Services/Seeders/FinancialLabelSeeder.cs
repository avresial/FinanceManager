using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class FinancialLabelSeeder(IFinancialLabelsRepository financialLabelsRepository, ILogger<FinancialLabelSeeder> logger) : ISeeder
{
    private readonly IReadOnlyCollection<string> _defaultLabels =
    [
        "Salary",
        "Undisclosed Income",
        "Groceries",
        "Rent",
        "Utilities",
        "Entertainment",
        "Transportation",
        "Healthcare",
        "Education",
        "Dining Out",
        "Travel"
    ];

    public async Task Seed(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await financialLabelsRepository.GetCount();
            if (count > 0)
            {
                logger.LogInformation("Financial labels already present: {Count}", count);
                return;
            }

            foreach (var label in _defaultLabels)
            {
                if (await financialLabelsRepository.Add(label, cancellationToken))
                    logger.LogInformation("Seeded default financial label: {Label}", label);
                else
                    logger.LogWarning("Failed to seed default financial label: {Label}", label);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding financial labels");
        }
    }
}