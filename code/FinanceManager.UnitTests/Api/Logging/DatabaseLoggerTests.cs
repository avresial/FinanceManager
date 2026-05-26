using FinanceManager.Api.Logging;
using FinanceManager.Domain.Entities.Logging;
using FinanceManager.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace FinanceManager.UnitTests.Api.Logging;

[Trait("Category", "Unit")]
public class DatabaseLoggerTests
{
    [Theory]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    public void IsEnabled_OnlyWarningAndAbove(LogLevel level, bool expected)
    {
        var logger = new DatabaseLogger("test", new RecordingQueue());
        Assert.Equal(expected, logger.IsEnabled(level));
    }

    [Fact]
    public void Log_BelowWarning_DoesNotEnqueue()
    {
        var queue = new RecordingQueue();
        var logger = new DatabaseLogger("test", queue);

        logger.LogInformation("hello");

        Assert.Empty(queue.Captured);
    }

    [Fact]
    public void Log_Warning_EnqueuesEntryWithMappedSeverity()
    {
        var queue = new RecordingQueue();
        var logger = new DatabaseLogger("MyCat", queue);

        logger.LogWarning(new EventId(42, "myEvent"), "something happened {x}", "y");

        Assert.Single(queue.Captured);
        var entry = queue.Captured[0];
        Assert.Equal(LogSeverity.Warning, entry.Level);
        Assert.Equal("MyCat", entry.Category);
        Assert.Equal("something happened y", entry.Message);
        Assert.Equal(42, entry.EventId);
        Assert.Equal("myEvent", entry.EventName);
    }

    [Fact]
    public void Log_WithException_CapturesExceptionString()
    {
        var queue = new RecordingQueue();
        var logger = new DatabaseLogger("MyCat", queue);

        logger.LogError(new InvalidOperationException("boom"), "bad");

        var entry = Assert.Single(queue.Captured);
        Assert.Equal(LogSeverity.Error, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.Contains("InvalidOperationException", entry.Exception);
    }

    [Fact]
    public void Suppression_PreventsEnqueueWithinScope()
    {
        var queue = new RecordingQueue();
        var logger = new DatabaseLogger("MyCat", queue);

        using (DatabaseLogger.BeginSuppression())
        {
            logger.LogError("inside");
        }

        logger.LogError("outside");

        Assert.Single(queue.Captured);
        Assert.Equal("outside", queue.Captured[0].Message);
    }

    [Fact]
    public void Log_TruncatesOverlongMessage()
    {
        var queue = new RecordingQueue();
        var logger = new DatabaseLogger("MyCat", queue);

        var longMessage = new string('x', 5000);
        logger.LogWarning(longMessage);

        var entry = Assert.Single(queue.Captured);
        Assert.Equal(4096, entry.Message.Length);
    }

    private sealed class RecordingQueue : ILogEntryQueue
    {
        public List<LogEntry> Captured { get; } = new();

        public ChannelReader<LogEntry> Reader => Channel.CreateUnbounded<LogEntry>().Reader;

        public bool TryEnqueue(LogEntry entry)
        {
            Captured.Add(entry);
            return true;
        }
    }
}