using System.Threading.Channels;

namespace FinanceManager.Api.Services;

public sealed class CurrencyImportJobChannel : ICurrencyImportJobChannel
{
    private readonly Channel<CurrencyImportJobRequest> _channel = Channel.CreateBounded<CurrencyImportJobRequest>(
        new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask QueueJob(CurrencyImportJobRequest request, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(request, cancellationToken);

    public IAsyncEnumerable<CurrencyImportJobRequest> ReadAll(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}