namespace FinanceManager.Api.Services;

public interface ILabelSetterChannel
{
    ValueTask QueueEntries(IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default);
    IAsyncEnumerable<IReadOnlyCollection<int>> ReadAll(CancellationToken cancellationToken);
}
