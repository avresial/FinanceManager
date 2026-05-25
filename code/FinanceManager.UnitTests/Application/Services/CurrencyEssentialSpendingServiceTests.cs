using FinanceManager.Application.Services.Currencies;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class CurrencyEssentialSpendingServiceTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly CurrencyEssentialSpendingService _service;
    private readonly DateTime _date = new(2026, 3, 10);

    public CurrencyEssentialSpendingServiceTests()
    {
        _service = new CurrencyEssentialSpendingService(_financialAccountRepositoryMock.Object);
    }

    [Fact]
    public async Task GetEssentialSpending_IncludesOnlyResolvedEssentialOutflows()
    {
        var userId = 1;
        var essentialLabel = CreateLabel("Rent", FinancialLabelClassificationCatalog.EssentialValue);
        var wantLabel = CreateLabel("Dining", FinancialLabelClassificationCatalog.WantValue);
        var account = new CurrencyAccount(userId, 1, "Household", AccountLabel.Cash);

        account.Add(new CurrencyAccountEntry(1, 1, _date, 1000m, -100m) { Labels = [essentialLabel] });
        account.Add(new CurrencyAccountEntry(1, 2, _date, 950m, -50m) { Labels = [essentialLabel, wantLabel, essentialLabel] });
        account.Add(new CurrencyAccountEntry(1, 3, _date, 940m, -10m) { Labels = [essentialLabel, wantLabel] });
        account.Add(new CurrencyAccountEntry(1, 4, _date, 1040m, 100m) { Labels = [essentialLabel] });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _service.GetEssentialSpending(userId, DefaultCurrency.PLN, _date, _date);

        Assert.Single(result);
        Assert.Equal(-150m, result[0].Value);
    }

    [Fact]
    public async Task GetEssentialSpending_TieResolution_ExcludesEntry()
    {
        var userId = 1;
        var essentialLabel = CreateLabel("Rent", FinancialLabelClassificationCatalog.EssentialValue);
        var wantLabel = CreateLabel("Dining", FinancialLabelClassificationCatalog.WantValue);
        var account = new CurrencyAccount(userId, 1, "Mixed", AccountLabel.Cash);

        account.Add(new CurrencyAccountEntry(1, 1, _date, 990m, -10m) { Labels = [essentialLabel, wantLabel] });

        _financialAccountRepositoryMock.Setup(repo => repo.GetAccounts<CurrencyAccount>(userId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { account }.ToAsyncEnumerable());

        var result = await _service.GetEssentialSpending(userId, DefaultCurrency.PLN, _date, _date);

        Assert.Single(result);
        Assert.Equal(0m, result[0].Value);
    }

    private static FinancialLabel CreateLabel(string name, string value) =>
        new()
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            Name = name,
            Classifications =
            [
                new FinancialLabelClassification
                {
                    Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind,
                    Value = value
                }
            ]
        };
}