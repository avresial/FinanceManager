using System.Threading.Channels;

namespace FinanceManager.Api.Services;

public sealed class LabelSetterChannel : ILabelSetterChannel
{
    private readonly Channel<LabelSetterRequest> _channel = Channel.CreateBounded<LabelSetterRequest>(
        new BoundedChannelOptions(512)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public ValueTask QueueEntries(int accountId, IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(new LabelSetterRequest(accountId, entryIds), cancellationToken);

    public IAsyncEnumerable<LabelSetterRequest> ReadAll(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
