using FinanceManager.Components.Features.FinancialAccounts.Models;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.FinancialAccounts.Services;

[Trait("Category", "Unit")]
public class InvestmentAccountDetailsSnapshotStoreTests
{
    private const string _key = "investment-account-details:1:2";

    private readonly Mock<ISnapshotService> _snapshots = new();

    private InvestmentAccountDetailsSnapshotStore CreateService() =>
        new(new SnapshotRefreshCoordinator(_snapshots.Object, NullLogger<SnapshotRefreshCoordinator>.Instance));

    [Fact]
    public async Task RefreshAsync_ReadFailure_PaintsNothingAndLoadsFresh()
    {
        _snapshots.Setup(x => x.GetAsync<InvestmentAccountDetailsSnapshot>(_key)).ThrowsAsync(new InvalidOperationException());

        var result = await Refresh(fresh: Model([1], []));

        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.RemoveAsync(_key), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_UnchangedContent_SkipsWrite()
    {
        GivenStoredSnapshot([1, 2], [1]);

        var result = await Refresh(fresh: Model([1, 2], [1]));

        Assert.Equal(SnapshotRefreshOutcome.Unchanged, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<InvestmentAccountDetailsSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_ChangedTrades_WritesUserAndAccountScopedKey()
    {
        GivenStoredSnapshot([1, 2], []);

        var result = await Refresh(fresh: Model([1, 3], []));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<InvestmentAccountDetailsSnapshot>(
            s => s.UserId == 1 && s.AccountId == 2 && s.Transactions.Select(t => t.Id).SequenceEqual(new long[] { 1, 3 }))), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_SameTradesButNewValuations_Repaints()
    {
        // A row leads with its gain/loss once priced and with its cash impact until then, so a
        // valuation arriving for an unchanged trade still changes what the user sees.
        GivenStoredSnapshot([1], []);

        var result = await Refresh(fresh: Model([1], [1]));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<InvestmentAccountDetailsSnapshot>(s => s.Valuations.Count == 1)), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_CurrencyChangedSinceSnapshot_Repaints()
    {
        // Valuations are expressed in the preferred currency, so a preference changed since the
        // snapshot was written must not leave last currency's amounts on screen.
        GivenStoredSnapshot([1], [1], DefaultCurrency.PLN);

        var result = await Refresh(fresh: Model([1], [1], DefaultCurrency.USD));

        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
        _snapshots.Verify(x => x.SetAsync(_key, It.Is<InvestmentAccountDetailsSnapshot>(
            s => s.Currency.Id == DefaultCurrency.USD.Id)), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_SnapshotFromAnotherAccount_IsNotPainted()
    {
        // A stale entry left under this key by a different account must never be rendered here.
        _snapshots.Setup(x => x.GetAsync<InvestmentAccountDetailsSnapshot>(_key))
            .ReturnsAsync(new InvestmentAccountDetailsSnapshot { UserId = 1, AccountId = 99, Transactions = [Transaction(1)] });

        var painted = false;
        var result = await Refresh(fresh: Model([1], []), onSnapshotPainted: _ =>
        {
            painted = true;
            return Task.CompletedTask;
        });

        Assert.False(painted);
        Assert.False(result.SnapshotPainted);
        Assert.Equal(SnapshotRefreshOutcome.Refreshed, result.Outcome);
    }

    private void GivenStoredSnapshot(List<long> transactionIds, List<long> valuedTransactionIds, Currency? currency = null) =>
        _snapshots.Setup(x => x.GetAsync<InvestmentAccountDetailsSnapshot>(_key))
            .ReturnsAsync(new InvestmentAccountDetailsSnapshot
            {
                UserId = 1,
                AccountId = 2,
                Name = "Brokerage",
                Currency = currency ?? DefaultCurrency.USD,
                Transactions = [.. transactionIds.Select(Transaction)],
                Valuations = [.. valuedTransactionIds.Select(Valuation)],
            });

    private Task<SnapshotRefreshResult<InvestmentAccountDetailsModel>> Refresh(
        InvestmentAccountDetailsModel? fresh,
        Func<InvestmentAccountDetailsModel, Task>? onSnapshotPainted = null) =>
        CreateService().RefreshAsync(1, 2, new RefreshVersionGate(), () => Task.FromResult(fresh), onSnapshotPainted);

    private static InvestmentAccountDetailsModel Model(List<long> transactionIds, List<long> valuedTransactionIds, Currency? currency = null) =>
        new(1, 2, "Brokerage", currency ?? DefaultCurrency.USD,
            [.. transactionIds.Select(Transaction)],
            [.. valuedTransactionIds.Select(Valuation)]);

    private static InvestmentTransactionDto Transaction(long id) =>
        new(id, 1, 2, 10, InvestmentTransactionType.Buy, 1m, 100m, "USD", new DateOnly(2024, 1, 10), null, null);

    private static InvestmentTransactionValuationDto Valuation(long transactionId) =>
        new(transactionId, 100m, 100m, 120m, 120m, 20m, 20m, "USD", true, true);
}