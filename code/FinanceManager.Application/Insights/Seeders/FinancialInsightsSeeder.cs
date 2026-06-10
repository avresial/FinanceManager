using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Insights.Seeders;

public class FinancialInsightsSeeder(IFinancialInsightsRepository financialInsightsRepository, IUserRepository userRepository,
    IConfiguration configuration, ILogger<FinancialInsightsSeeder> logger) : ISeeder
{
    // Hand-written demo insights so the dashboard "Financial insights" card looks populated for the guest/demo
    // account, where the AI generator is not run. Tags must match the lowercase chips the carousel renders.
    private static readonly (string Title, string Message, string Tags)[] _demoInsights =
    [
        ("Spending stayed within budget",
            "Your cash outflows over the last month were about 8% lower than your six-month average, leaving more room to save.",
            "cashflow,summary"),
        ("Portfolio leans towards equities",
            "Stocks make up the largest share of your invested assets. Review whether this allocation still matches your risk tolerance.",
            "portfolio,allocation,risk"),
        ("Your bond ladder is building steadily",
            "Regular monthly bond purchases are creating a steady stream of future maturities, smoothing out reinvestment risk over time.",
            "portfolio,trend"),
    ];

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

        await SeedForGuest(guestUser.UserId, cancellationToken);
    }

    // Seeds the demo insights for a specific (per-session) guest user. Called from GuestAccountSeeder so the
    // insights card is populated for the throwaway in-memory guest DB created at login.
    public async Task SeedForGuest(int guestUserId, CancellationToken cancellationToken = default)
    {
        if (await financialInsightsRepository.GetCountByUser(guestUserId, cancellationToken) > 0)
            return;

        var now = DateTime.UtcNow.Date;
        var insights = new List<FinancialInsight>();

        for (var i = 0; i < _demoInsights.Length; i++)
        {
            var (title, message, tags) = _demoInsights[i];
            insights.Add(new FinancialInsight
            {
                UserId = guestUserId,
                AccountId = null,
                Title = title,
                Message = message,
                Tags = tags,
                CreatedAt = now.AddDays(-(_demoInsights.Length - 1) + i)
            });
        }

        await financialInsightsRepository.AddRange(insights, cancellationToken);
    }
}