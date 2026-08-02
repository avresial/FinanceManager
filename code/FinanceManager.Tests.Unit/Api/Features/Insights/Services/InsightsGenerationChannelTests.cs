using FinanceManager.Api.Features.Insights.Services;
using FinanceManager.Tests.Unit.Shared.Time;

namespace FinanceManager.Tests.Unit.Api.Features.Insights.Services;

[Trait("Category", "Unit")]
public class InsightsGenerationChannelTests
{
    private static readonly DateTime _utcNow = new(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);

    private readonly FakeDateTimeProvider _dateTimeProvider = new(_utcNow);
    private readonly InsightsGenerationChannel _channel;

    public InsightsGenerationChannelTests() => _channel = new InsightsGenerationChannel(_dateTimeProvider);

    [Fact]
    public async Task QueueUser_PublishesUser()
    {
        var queued = await _channel.QueueUser(1, TestContext.Current.CancellationToken);

        Assert.True(queued);
        Assert.Equal([1], await Drain());
    }

    [Fact]
    public async Task QueueUser_DropsRepeatedRequestForSameUser()
    {
        await _channel.QueueUser(1, TestContext.Current.CancellationToken);
        _dateTimeProvider.Advance(TimeSpan.FromMinutes(59));

        var queued = await _channel.QueueUser(1, TestContext.Current.CancellationToken);

        Assert.False(queued);
        Assert.Equal([1], await Drain());
    }

    [Fact]
    public async Task QueueUser_AcceptsSameUserAgainAfterCooldown()
    {
        await _channel.QueueUser(1, TestContext.Current.CancellationToken);
        _dateTimeProvider.Advance(TimeSpan.FromHours(1));

        var queued = await _channel.QueueUser(1, TestContext.Current.CancellationToken);

        Assert.True(queued);
        Assert.Equal([1, 1], await Drain());
    }

    [Fact]
    public async Task QueueUser_KeepsCooldownPerUser()
    {
        await _channel.QueueUser(1, TestContext.Current.CancellationToken);

        var queued = await _channel.QueueUser(2, TestContext.Current.CancellationToken);

        Assert.True(queued);
        Assert.Equal([1, 2], await Drain());
    }

    [Fact]
    public async Task QueueUser_ReleasesCooldown_WhenWriteFails()
    {
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await _channel.QueueUser(1, canceled.Token));
        var queued = await _channel.QueueUser(1, TestContext.Current.CancellationToken);

        Assert.True(queued);
        Assert.Equal([1], await Drain());
    }

    /// <summary>Reads everything published so far; the channel stays open, so the read is bounded by a short timeout.</summary>
    private async Task<List<int>> Drain()
    {
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        List<int> received = [];
        try
        {
            await foreach (var userId in _channel.ReadAll(cancellationTokenSource.Token))
                received.Add(userId);
        }
        catch (OperationCanceledException)
        {
        }

        return received;
    }
}