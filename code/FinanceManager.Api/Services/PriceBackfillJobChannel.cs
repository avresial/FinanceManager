using System.Threading.Channels;

namespace FinanceManager.Api.Services;

public sealed class PriceBackfillJobChannel : IPriceBackfillJobChannel
{
    private readonly Channel<PriceBackfillJobRequest> _channel = Channel.CreateBounded<PriceBackfillJobRequest>(
        new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });

    public bool TryQueueJob(PriceBackfillJobRequest request) =>
        _channel.Writer.TryWrite(request);

    public IAsyncEnumerable<PriceBackfillJobRequest> ReadAll(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}