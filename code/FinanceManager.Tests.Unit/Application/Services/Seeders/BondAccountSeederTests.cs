using FinanceManager.Application.FinancialAccounts.Bond.Seeders;
using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Seeders;

[Collection("Application")]
[Trait("Category", "Unit")]
public class BondAccountSeederTests
{
    private static readonly BondDetails _bond = new()
    {
        Id = 7,
        Name = "EDO1235",
        Issuer = "Ministry of Finance - Poland",
        Type = BondType.InflationBond,
        Currency = new Currency(1, "PLN", "zł"),
        UnitValue = 100m,
    };

    private readonly Mock<IBondTransactionReader> _bondReader = new();
    private readonly Mock<IBondAccountEntryRepository<BondAccountEntry>> _entryRepository = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepository = new();
    private readonly List<BondAccountEntry> _savedEntries = [];
    private readonly BondAccountSeeder _seeder;

    public BondAccountSeederTests()
    {
        _bondReader
            .Setup(r => r.HasAnyEntries(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _bondDetailsRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(new[] { _bond }.ToAsyncEnumerable());

        _entryRepository
            .Setup(r => r.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<BondAccountEntry>, bool, CancellationToken>((entries, _, _) => _savedEntries.AddRange(entries))
            .ReturnsAsync(true);

        _seeder = new BondAccountSeeder(
            _bondReader.Object,
            _entryRepository.Object,
            _bondDetailsRepository.Object,
            NullLogger<BondAccountSeeder>.Instance);
    }

    [Fact]
    public async Task Seed_BuysOnlyOnBondInvestmentDays()
    {
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 7, 20);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var entries = GetBondEntries();
        // Feb–Jul: one buy on the bond day of each month after the opening day.
        Assert.Equal(6, entries.Count);
        Assert.All(entries, e => Assert.Equal(GuestInvestmentPlan.BondDayOfMonth, e.PostingDate.Day));
        Assert.All(entries, e => Assert.Equal(_bond.Id, e.BondDetailsId));
    }

    [Fact]
    public async Task Seed_BuysUnitsMatchingTheMonthlyBondInvestment()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 6, 30);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var expectedUnits = Math.Round(GuestInvestmentPlan.BondMonthlyAmount / _bond.UnitValue, 4, MidpointRounding.AwayFromZero);
        var entries = GetBondEntries();
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal(expectedUnits, e.ValueChange));
        // Each purchase's cash cost (units x unit value) equals the monthly bond investment the cash account spends.
        Assert.All(entries, e => Assert.Equal(GuestInvestmentPlan.BondMonthlyAmount, e.ValueChange * _bond.UnitValue));
    }

    [Fact]
    public async Task Seed_BuysFractionalUnitsWhenUnitValueDoesNotDivideMonthlyAmount()
    {
        // 300 / 90 = 3.3333… — a unit value that doesn't divide the monthly amount is still bought fractionally
        // so the cash debit and the value of the units acquired stay consistent.
        var fractionalBond = new BondDetails
        {
            Id = 9,
            Name = "EDO-frac",
            Issuer = "Ministry of Finance - Poland",
            Type = BondType.InflationBond,
            Currency = new Currency(1, "PLN", "zł"),
            UnitValue = 90m,
        };
        _bondDetailsRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(new[] { fractionalBond }.ToAsyncEnumerable());

        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 3, 31);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var entries = GetBondEntries();
        Assert.NotEmpty(entries);
        // Units are fractional, not floored to a whole number.
        Assert.All(entries, e => Assert.NotEqual(Math.Floor(e.ValueChange), e.ValueChange));
        // The value of the units acquired still matches the monthly amount within rounding.
        Assert.All(entries, e => Assert.True(Math.Abs(e.ValueChange * fractionalBond.UnitValue - GuestInvestmentPlan.BondMonthlyAmount) < 0.05m));
    }

    [Fact]
    public async Task Seed_WritesEntriesOwnedByTheUser_WithoutAnAccount_InOneBatch()
    {
        await _seeder.Seed(userId: -9, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30), TestContext.Current.CancellationToken);

        var entries = GetBondEntries();
        Assert.True(entries.Count > 1);
        Assert.All(entries, e =>
        {
            Assert.Null(e.AccountId);
            Assert.Equal(-9, e.UserId);
            Assert.Equal(_bond.Name, e.Description);
            Assert.Equal(DateTimeKind.Utc, e.PostingDate.Kind);
        });
        _entryRepository.Verify(r => r.Add(It.IsAny<IEnumerable<BondAccountEntry>>(), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Seed_SkipsWhenTheUserAlreadyHasBondEntries_OrIsNotAUser(bool hasEntries, int userId)
    {
        _bondReader.Setup(r => r.HasAnyEntries(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(hasEntries);

        await _seeder.Seed(userId, new DateTime(2026, 1, 1), new DateTime(2026, 3, 1), TestContext.Current.CancellationToken);

        Assert.Empty(_savedEntries);
    }

    [Fact]
    public async Task Seed_SkipsWhenNoBondDetailsAvailable()
    {
        _bondDetailsRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<BondDetails>());

        await _seeder.Seed(userId: 1, new DateTime(2026, 1, 1), new DateTime(2026, 3, 1), TestContext.Current.CancellationToken);

        Assert.Empty(_savedEntries);
    }

    private List<BondAccountEntry> GetBondEntries() => _savedEntries;
}