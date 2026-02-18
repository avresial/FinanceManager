using System.Threading.Channels;

namespace FinanceManager.Api.Services;

public sealed class LabelSetterChannel : ILabelSetterChannel
{
    private readonly Channel<IReadOnlyCollection<int>> _channel = Channel.CreateBounded<IReadOnlyCollection<int>>(
        new BoundedChannelOptions(512)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public ValueTask QueueEntries(IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(entryIds, cancellationToken);

    public IAsyncEnumerable<IReadOnlyCollection<int>> ReadAll(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
