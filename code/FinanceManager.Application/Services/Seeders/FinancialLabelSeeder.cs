using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class FinancialLabelSeeder(IFinancialLabelsRepository financialLabelsRepository, ILogger<FinancialLabelSeeder> logger) : ISeeder
{
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

            var added = await financialLabelsRepository.Add("Salary");
            if (added)
                logger.LogInformation("Seeded default financial label: Salary");
            else
                logger.LogWarning("Failed to seed default financial label: Salary");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding financial labels");
        }
    }
}