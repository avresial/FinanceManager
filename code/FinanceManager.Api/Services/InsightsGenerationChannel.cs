using System.Threading.Channels;

namespace FinanceManager.Api.Services;

public sealed class InsightsGenerationChannel : IInsightsGenerationChannel
{
    private readonly Channel<int> _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(256)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public ValueTask QueueUser(int userId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(userId, cancellationToken);

    public IAsyncEnumerable<int> ReadAll(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}