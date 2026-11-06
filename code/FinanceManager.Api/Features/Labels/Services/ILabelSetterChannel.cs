namespace FinanceManager.Api.Features.Labels.Services;

public interface ILabelSetterChannel
{
    ValueTask QueueEntries(int accountId, IReadOnlyCollection<long> entryIds, CancellationToken cancellationToken = default);
    IAsyncEnumerable<LabelSetterRequest> ReadAll(CancellationToken cancellationToken);
}

public sealed record LabelSetterRequest(int AccountId, IReadOnlyCollection<long> EntryIds);