using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Infrastructure.Features.Administration.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.Administration.Repositories;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class LogEntryRepositoryTests
{
    [Fact]
    public async Task GetPaged_AppliesFiltersAndReturnsNewestFirst()
    {
        await using var context = CreateContext();
        var baseTime = new DateTime(2026, 8, 27, 1, 0, 0, DateTimeKind.Utc);
        context.LogEntries.AddRange(
            new LogEntry { Id = 1, TimestampUtc = baseTime, Level = LogSeverity.Warning, Category = "Provider", Message = "old Alpha" },
            new LogEntry { Id = 2, TimestampUtc = baseTime.AddHours(1), Level = LogSeverity.Warning, Category = "Provider", Message = "new Alpha" },
            new LogEntry { Id = 3, TimestampUtc = baseTime.AddHours(2), Level = LogSeverity.Error, Category = "Provider", Message = "error Alpha" },
            new LogEntry { Id = 4, TimestampUtc = baseTime.AddHours(3), Level = LogSeverity.Warning, Category = "Provider", Message = "new Beta" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new LogEntryRepository(context);
        var (items, total) = await repository.GetPaged(
            skip: 0,
            take: 10,
            levels: [LogSeverity.Warning],
            fromUtc: baseTime.AddMinutes(30),
            toUtc: baseTime.AddHours(3),
            search: "alpha",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, total);
        var item = Assert.Single(items);
        Assert.Equal(2, item.Id);
    }

    [Fact]
    public async Task GetPaged_PaginatesAfterOrderingByTimestampAndId()
    {
        await using var context = CreateContext();
        var timestamp = new DateTime(2026, 8, 27, 1, 0, 0, DateTimeKind.Utc);
        context.LogEntries.AddRange(
            new LogEntry { Id = 1, TimestampUtc = timestamp },
            new LogEntry { Id = 2, TimestampUtc = timestamp },
            new LogEntry { Id = 3, TimestampUtc = timestamp.AddHours(1) });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (items, total) = await new LogEntryRepository(context).GetPaged(
            skip: 1,
            take: 1,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, total);
        Assert.Equal(2, Assert.Single(items).Id);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}