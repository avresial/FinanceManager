using FinanceManager.Application.FinancialAccounts.Bond.Seeders;
using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
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

    private readonly Mock<IFinancialAccountRepository> _accountRepository = new();
    private readonly Mock<IAccountRepository<BondAccount>> _bondAccountRepository = new();
    private readonly Mock<IBondDetailsRepository> _bondDetailsRepository = new();
    private readonly List<BondAccount> _savedAccounts = [];
    private readonly BondAccountSeeder _seeder;

    public BondAccountSeederTests()
    {
        _bondAccountRepository
            .Setup(r => r.GetAvailableAccounts(It.IsAny<int>()))
            .Returns(AsyncEnumerable.Empty<AvailableAccount>());

        _bondDetailsRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(new[] { _bond }.ToAsyncEnumerable());

        _accountRepository
            .Setup(r => r.AddAccount(It.IsAny<BondAccount>()))
            .Callback<BondAccount>(a => _savedAccounts.Add(a))
            .ReturnsAsync(0);

        _seeder = new BondAccountSeeder(
            _accountRepository.Object,
            _bondAccountRepository.Object,
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
    public async Task Seed_AccumulatesUnitsAndAssignsDistinctIds()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 6, 30);

        await _seeder.Seed(userId: 1, start, end, TestContext.Current.CancellationToken);

        var entries = GetBondEntries().OrderBy(e => e.PostingDate).ToList();
        Assert.True(entries.Count > 1);
        // BondAccount.Add silently drops entries that share an EntryId, so distinct ids guard against lost purchases.
        Assert.Equal(entries.Count, entries.Select(e => e.EntryId).Distinct().Count());

        decimal expected = 0;
        foreach (var entry in entries)
        {
            expected += entry.ValueChange;
            Assert.Equal(expected, entry.Value);
        }
    }

    [Fact]
    public async Task Seed_SkipsWhenBondAccountAlreadyExists()
    {
        _bondAccountRepository
            .Setup(r => r.GetAvailableAccounts(It.IsAny<int>()))
            .Returns(new[] { new AvailableAccount(1, "Existing") }.ToAsyncEnumerable());

        await _seeder.Seed(userId: 1, new DateTime(2026, 1, 1), new DateTime(2026, 3, 1), TestContext.Current.CancellationToken);

        Assert.Empty(_savedAccounts);
    }

    [Fact]
    public async Task Seed_SkipsWhenNoBondDetailsAvailable()
    {
        _bondDetailsRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(AsyncEnumerable.Empty<BondDetails>());

        await _seeder.Seed(userId: 1, new DateTime(2026, 1, 1), new DateTime(2026, 3, 1), TestContext.Current.CancellationToken);

        Assert.Empty(_savedAccounts);
    }

    private List<BondAccountEntry> GetBondEntries() =>
        Assert.Single(_savedAccounts).Entries.ToList();
}