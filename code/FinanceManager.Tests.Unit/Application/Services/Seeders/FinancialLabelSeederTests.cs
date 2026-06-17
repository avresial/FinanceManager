using FinanceManager.Application.Labels.Seeders;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services.Seeders;

[Collection("Application")]
[Trait("Category", "Unit")]
public class FinancialLabelSeederTests
{
    private readonly Mock<IFinancialLabelsRepository> _labelsRepository = new();
    private readonly List<(int LabelId, string Kind, string Value)> _classifications = [];
    private readonly FinancialLabelSeeder _seeder;

    // Mirrors the default label set so the name-seeding loop is a no-op and the test focuses on classifications.
    private readonly List<FinancialLabel> _existingLabels =
    [
        new() { Id = 1, Name = "Undisclosed Expense" },
        new() { Id = 2, Name = "Undisclosed Income" },
        new() { Id = 3, Name = "Salary" },
        new() { Id = 4, Name = "Groceries" },
        new() { Id = 5, Name = "Rent" },
        new() { Id = 6, Name = "Utilities" },
        new() { Id = 7, Name = "Entertainment" },
        new() { Id = 8, Name = "Subscription" },
        new() { Id = 9, Name = "Transportation" },
        new() { Id = 10, Name = "Healthcare" },
        new() { Id = 11, Name = "Education" },
        new() { Id = 12, Name = "Dining Out" },
        new() { Id = 13, Name = "Travel" },
        new() { Id = 14, Name = "Investment" },
        new() { Id = 15, Name = "Loan" },
        new() { Id = 16, Name = WellKnownFinancialLabels.NoMatch },
    ];

    public FinancialLabelSeederTests()
    {
        _labelsRepository
            .Setup(r => r.GetLabels(It.IsAny<CancellationToken>()))
            .Returns(() => _existingLabels.ToAsyncEnumerable());

        _labelsRepository
            .Setup(r => r.AddClassification(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, string, CancellationToken>((id, kind, value, _) =>
            {
                _classifications.Add((id, kind, value));
                _existingLabels.Single(l => l.Id == id).Classifications.Add(
                    new FinancialLabelClassification { LabelId = id, Kind = kind, Value = value });
            })
            .ReturnsAsync(true);

        _seeder = new FinancialLabelSeeder(_labelsRepository.Object, NullLogger<FinancialLabelSeeder>.Instance);
    }

    [Fact]
    public async Task Seed_AssignsExpectedSpendingNecessityClassifications()
    {
        await _seeder.Seed(TestContext.Current.CancellationToken);

        Assert.All(_classifications, c => Assert.Equal(FinancialLabelClassificationCatalog.SpendingNecessityKind, c.Kind));

        var byLabel = _classifications.ToDictionary(c => _existingLabels.Single(l => l.Id == c.LabelId).Name, c => c.Value);

        Assert.Equal(FinancialLabelClassificationCatalog.EssentialValue, byLabel["Groceries"]);
        Assert.Equal(FinancialLabelClassificationCatalog.EssentialValue, byLabel["Rent"]);
        Assert.Equal(FinancialLabelClassificationCatalog.EssentialValue, byLabel["Utilities"]);
        Assert.Equal(FinancialLabelClassificationCatalog.EssentialValue, byLabel["Transportation"]);
        Assert.Equal(FinancialLabelClassificationCatalog.EssentialValue, byLabel["Healthcare"]);
        Assert.Equal(FinancialLabelClassificationCatalog.EssentialValue, byLabel["Education"]);
        Assert.Equal(FinancialLabelClassificationCatalog.EssentialValue, byLabel["Loan"]);
        Assert.Equal(FinancialLabelClassificationCatalog.WantValue, byLabel["Entertainment"]);
        Assert.Equal(FinancialLabelClassificationCatalog.WantValue, byLabel["Subscription"]);
        Assert.Equal(FinancialLabelClassificationCatalog.WantValue, byLabel["Dining Out"]);
        Assert.Equal(FinancialLabelClassificationCatalog.WantValue, byLabel["Travel"]);
        Assert.Equal(FinancialLabelClassificationCatalog.InvestmentValue, byLabel["Investment"]);
        Assert.Equal(FinancialLabelClassificationCatalog.UnknownValue, byLabel["Undisclosed Expense"]);
    }

    [Fact]
    public async Task Seed_LeavesIncomeAndSentinelLabelsUnclassified()
    {
        await _seeder.Seed(TestContext.Current.CancellationToken);

        var classifiedNames = _classifications
            .Select(c => _existingLabels.Single(l => l.Id == c.LabelId).Name)
            .ToHashSet();

        Assert.DoesNotContain("Salary", classifiedNames);
        Assert.DoesNotContain("Undisclosed Income", classifiedNames);
        Assert.DoesNotContain(WellKnownFinancialLabels.NoMatch, classifiedNames);
    }

    [Fact]
    public async Task Seed_DoesNotOverwriteLabelsThatAlreadyHaveASpendingNecessityClassification()
    {
        // An admin has already classified Rent as a Want; the seeder must leave that choice untouched.
        _existingLabels.Single(l => l.Name == "Rent").Classifications.Add(new FinancialLabelClassification
        {
            LabelId = 5,
            Kind = FinancialLabelClassificationCatalog.SpendingNecessityKind,
            Value = FinancialLabelClassificationCatalog.WantValue
        });

        await _seeder.Seed(TestContext.Current.CancellationToken);

        _labelsRepository.Verify(
            r => r.AddClassification(5, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}