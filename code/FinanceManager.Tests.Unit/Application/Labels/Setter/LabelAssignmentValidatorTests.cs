using FinanceManager.Application.Labels.Setter;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Unit.Application.Labels.Setter;

[Collection("Application")]
[Trait("Category", "Unit")]
public class LabelAssignmentValidatorTests
{
    private readonly LabelAssignmentValidator _validator = new(NullLogger<LabelAssignmentValidator>.Instance);

    private static readonly HashSet<string> _labelsWithSentinel =
        new(["Groceries", "Rent", WellKnownFinancialLabels.NoMatch], StringComparer.Ordinal);

    [Fact]
    public void Validate_KnownLabelAndEntryId_IsAccepted()
    {
        var result = _validator.Validate([new LabelAssignment(1, "Groceries")], [1, 2], _labelsWithSentinel);

        Assert.Equal(new Dictionary<int, string> { [1] = "Groceries" }, result);
    }

    [Fact]
    public void Validate_UnknownEntryId_IsIgnored()
    {
        var result = _validator.Validate([new LabelAssignment(99, "Groceries")], [1, 2], _labelsWithSentinel);

        Assert.Empty(result);
    }

    [Fact]
    public void Validate_NullEntryId_IsIgnored()
    {
        var result = _validator.Validate([new LabelAssignment(null, "Groceries")], [1, 2], _labelsWithSentinel);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown Label")]
    public void Validate_MissingOrUnknownLabel_FallsBackToNoMatchSentinel(string? labelName)
    {
        var result = _validator.Validate([new LabelAssignment(1, labelName)], [1], _labelsWithSentinel);

        Assert.Equal(new Dictionary<int, string> { [1] = WellKnownFinancialLabels.NoMatch }, result);
    }

    [Fact]
    public void Validate_UnknownLabelWithoutSentinelDefined_IsSkipped()
    {
        var labelsWithoutSentinel = new HashSet<string>(["Groceries"], StringComparer.Ordinal);

        var result = _validator.Validate([new LabelAssignment(1, "Unknown Label")], [1], labelsWithoutSentinel);

        Assert.Empty(result);
    }
}