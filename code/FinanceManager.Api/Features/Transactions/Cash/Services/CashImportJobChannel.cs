using System.Threading.Channels;

namespace FinanceManager.Api.Features.Transactions.Cash.Services;

public sealed class CashImportJobChannel : ICashImportJobChannel
{
    private readonly Channel<CashImportJobRequest> _channel = Channel.CreateBounded<CashImportJobRequest>(
        new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask QueueJob(CashImportJobRequest request, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(request, cancellationToken);

    public IAsyncEnumerable<CashImportJobRequest> ReadAll(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}