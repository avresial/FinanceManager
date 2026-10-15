using FinanceManager.Application.Identity.Seeders;

namespace FinanceManager.Tests.Unit.Application.Services.Seeders;

[Collection("Application")]
[Trait("Category", "Unit")]
public class GuestDemoSeedTests
{
    private static readonly DateOnly _date = new(2026, 10, 14);

    [Fact]
    public void Seed_EncodesDateAsYyyyMmDd() =>
        Assert.Equal(20261014, new GuestDemoSeed(_date).Seed);

    [Fact]
    public void CreateRandom_SameDateAndSalt_ProducesIdenticalSequences() =>
        Assert.Equal(Sequence(new GuestDemoSeed(_date), 1), Sequence(new GuestDemoSeed(_date), 1));

    [Fact]
    public void CreateRandom_DifferentDates_ProducesDifferentSequences() =>
        Assert.NotEqual(Sequence(new GuestDemoSeed(_date), 1), Sequence(new GuestDemoSeed(_date.AddDays(1)), 1));

    [Fact]
    public void CreateRandom_DifferentSalts_ProducesDifferentSequences()
    {
        var seed = new GuestDemoSeed(_date);

        Assert.NotEqual(Sequence(seed, GuestDemoSeed.CashSalt), Sequence(seed, GuestDemoSeed.InvestmentPricesSalt));
    }

    [Fact]
    public void Seed_AcrossUtcMidnight_DiffersBetweenDays()
    {
        var beforeMidnight = new GuestDemoSeed(DateOnly.FromDateTime(new DateTime(2026, 10, 14, 23, 59, 59, DateTimeKind.Utc)));
        var afterMidnight = new GuestDemoSeed(DateOnly.FromDateTime(new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc)));

        Assert.NotEqual(beforeMidnight.Seed, afterMidnight.Seed);
    }

    private static int[] Sequence(GuestDemoSeed seed, int salt)
    {
        var random = seed.CreateRandom(salt);
        return [.. Enumerable.Range(0, 20).Select(_ => random.Next())];
    }
}