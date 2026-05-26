using FinanceManager.Domain.Entities.Logging;
using System.Threading.Channels;

namespace FinanceManager.Api.Logging;

public interface ILogEntryQueue
{
    bool TryEnqueue(LogEntry entry);
    ChannelReader<LogEntry> Reader { get; }
}

public sealed class LogEntryQueue : ILogEntryQueue
{
    private readonly Channel<LogEntry> _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(2048)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false,
    });

    public ChannelReader<LogEntry> Reader => _channel.Reader;

    public bool TryEnqueue(LogEntry entry) => _channel.Writer.TryWrite(entry);
}