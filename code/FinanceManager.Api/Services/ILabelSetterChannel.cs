namespace FinanceManager.Api.Services;

public interface ILabelSetterChannel
{
    ValueTask QueueEntries(int accountId, IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default);
    IAsyncEnumerable<LabelSetterRequest> ReadAll(CancellationToken cancellationToken);
}

public sealed record LabelSetterRequest(int AccountId, IReadOnlyCollection<int> EntryIds);
