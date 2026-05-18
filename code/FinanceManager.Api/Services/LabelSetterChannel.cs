using System.Threading.Channels;

namespace FinanceManager.Api.Services;

public sealed class LabelSetterChannel(ILabelSetterProgressTracker progressTracker) : ILabelSetterChannel
{
    private readonly Channel<LabelSetterRequest> _channel = Channel.CreateBounded<LabelSetterRequest>(
        new BoundedChannelOptions(512)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    private int _queuedCount;

    public async ValueTask QueueEntries(int accountId, IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(new LabelSetterRequest(accountId, entryIds), cancellationToken);
        progressTracker.SetQueuedJobsCount(Interlocked.Increment(ref _queuedCount));
    }

    public async IAsyncEnumerable<LabelSetterRequest> ReadAll([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            progressTracker.SetQueuedJobsCount(Interlocked.Decrement(ref _queuedCount));
            yield return request;
        }
    }
}