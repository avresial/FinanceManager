using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Services.Seeders;

public class FinancialInsightsSeeder(IFinancialInsightsRepository financialInsightsRepository, IUserRepository userRepository,
    IConfiguration configuration, ILogger<FinancialInsightsSeeder> logger) : ISeeder
{
    private static readonly string[] TagPool = ["summary", "portfolio", "cashflow", "allocation", "risk", "trend"];

    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var guestLogin = configuration["DefaultUser:Login"];
        if (string.IsNullOrWhiteSpace(guestLogin))
        {
            logger.LogWarning("DefaultUser:Login not configured. Skipping insights seeding.");
            return;
        }

        var guestUser = await userRepository.GetUser(guestLogin);
        if (guestUser is null)
        {
            logger.LogWarning("Guest user not found. Skipping insights seeding.");
            return;
        }

        if (await financialInsightsRepository.GetCountByUser(guestUser.UserId, cancellationToken) > 0)
            return;

        var now = DateTime.UtcNow.Date;
        var insights = new List<FinancialInsight>();

        for (var i = 0; i < 3; i++)
        {
            insights.Add(new FinancialInsight
            {
                UserId = guestUser.UserId,
                AccountId = null,
                Title = "Lorem ipsum",
                Message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
                Tags = GenerateTags(),
                CreatedAt = now.AddDays(-2 + i)
            });
        }

        await financialInsightsRepository.AddRange(insights, cancellationToken);
    }

    private static string GenerateTags()
    {
        var first = TagPool[Random.Shared.Next(TagPool.Length)];
        var second = TagPool[Random.Shared.Next(TagPool.Length)];
        return first == second ? first : $"{first},{second}";
    }
}