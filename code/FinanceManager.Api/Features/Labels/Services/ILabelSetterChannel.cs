namespace FinanceManager.Api.Features.Labels.Services;

public interface ILabelSetterChannel
{
    ValueTask QueueEntries(int userId, IReadOnlyCollection<long> entryIds, CancellationToken cancellationToken = default);
    IAsyncEnumerable<LabelSetterRequest> ReadAll(CancellationToken cancellationToken);
}