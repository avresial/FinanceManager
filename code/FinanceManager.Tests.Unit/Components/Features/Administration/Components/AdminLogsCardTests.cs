using Bunit;
using FinanceManager.Components.Features.Administration.Components;
using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Administration.Logging;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Administration.Components;

public sealed class AdminLogsCardTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
    private static readonly DateTime _baseTime = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StoredSnapshot_PaintsBeforeRestResponse_AndRestAlwaysRuns()
    {
        var snapshots = SnapshotsWith(7, Entry(1), Entry(2));
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler);

        var cut = context.Render<AdminLogsCard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("msg-1", cut.Markup);
            Assert.Contains("msg-2", cut.Markup);
        }, _timeout);
        Assert.Equal(1, handler.Requests);
        response.SetResult(Json(Entry(2), Entry(1)));
    }

    [Fact]
    public async Task ChangedRestResult_RepaintsAndWritesMergedList()
    {
        var snapshots = SnapshotsWith(7, Entry(1));
        var coordinator = new SignalingCoordinator(snapshots.Object);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler, coordinator);
        var cut = context.Render<AdminLogsCard>();
        cut.WaitForAssertion(() => Assert.Contains("msg-1", cut.Markup), _timeout);

        response.SetResult(Json(Entry(4), Entry(1)));
        await coordinator.NextCompletion();

        cut.WaitForAssertion(() => Assert.Contains("msg-4", cut.Markup), _timeout);
        snapshots.Verify(s => s.SetAsync("admin-logs:7", It.Is<AdminLogsCardSnapshot>(
            snapshot => snapshot.UserId == 7 && Ids(snapshot) == "4,1")), Times.Once);
    }

    [Fact]
    public async Task UnchangedRestResult_DoesNotWrite()
    {
        var snapshots = SnapshotsWith(7, Entry(2), Entry(1));
        var coordinator = new SignalingCoordinator(snapshots.Object);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler, coordinator);
        var cut = context.Render<AdminLogsCard>();
        cut.WaitForAssertion(() => Assert.Contains("msg-2", cut.Markup), _timeout);

        response.SetResult(Json(Entry(2), Entry(1)));
        await coordinator.NextCompletion();

        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AdminLogsCardSnapshot>()), Times.Never);
        Assert.Contains("msg-2", cut.Markup);
    }

    [Fact]
    public async Task LiveBatch_UpdatesRenderAndWritesSnapshot_CappedAtFiveNewestFirst()
    {
        var snapshots = SnapshotsWith(7, Entry(5), Entry(4), Entry(3), Entry(2), Entry(1));
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminLogsCard>();
        cut.WaitForAssertion(() => Assert.Contains("msg-1", cut.Markup), _timeout);

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveBatchAsync([Dto(6), Dto(7), Dto(5)]));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("msg-7", cut.Markup);
            Assert.DoesNotContain("msg-1", cut.Markup);
            Assert.DoesNotContain("msg-2", cut.Markup);
        }, _timeout);
        snapshots.Verify(s => s.SetAsync("admin-logs:7", It.Is<AdminLogsCardSnapshot>(
            snapshot => Ids(snapshot) == "7,6,5,4,3")), Times.Once);
        response.SetResult(Json(Entry(5), Entry(4), Entry(3), Entry(2), Entry(1)));
    }

    [Fact]
    public async Task NullOrEmptyLiveBatch_IsIgnored()
    {
        var snapshots = SnapshotsWith(7, Entry(1));
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminLogsCard>();
        cut.WaitForAssertion(() => Assert.Contains("msg-1", cut.Markup), _timeout);

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveBatchAsync(null!));
        await cut.InvokeAsync(() => cut.Instance.ApplyLiveBatchAsync([]));

        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AdminLogsCardSnapshot>()), Times.Never);
        response.SetResult(Json(Entry(1)));
    }

    [Fact]
    public async Task LiveEntryDuringRestRead_IsNotDroppedByLateRestResult()
    {
        var snapshots = SnapshotsWith(7, Entry(3), Entry(2), Entry(1));
        var coordinator = new SignalingCoordinator(snapshots.Object);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler, coordinator);
        var cut = context.Render<AdminLogsCard>();
        cut.WaitForAssertion(() => Assert.Contains("msg-3", cut.Markup), _timeout);

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveBatchAsync([Dto(10)]));
        response.SetResult(Json(Entry(3), Entry(2), Entry(1))); // stale: does not know about 10
        await coordinator.NextCompletion();

        Assert.Contains("msg-10", cut.Markup);
        Assert.Contains("msg-3", cut.Markup);
        snapshots.Verify(s => s.SetAsync("admin-logs:7", It.Is<AdminLogsCardSnapshot>(
            snapshot => Ids(snapshot) == "10,3,2,1")), Times.AtLeastOnce);
        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.Is<AdminLogsCardSnapshot>(
            snapshot => !snapshot.Entries.Any(e => e.Id == 10))), Times.Never);
    }

    [Fact]
    public async Task LiveEntryBeforeSnapshotPaint_IsKeptWhenSnapshotArrivesLate()
    {
        var snapshots = new Mock<ISnapshotService>();
        var storedRead = new TaskCompletionSource<AdminLogsCardSnapshot?>();
        snapshots.Setup(s => s.GetAsync<AdminLogsCardSnapshot>("admin-logs:7")).Returns(storedRead.Task);
        var coordinator = new SignalingCoordinator(snapshots.Object);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler, coordinator);
        var cut = context.Render<AdminLogsCard>();

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveBatchAsync([Dto(10)]));
        storedRead.SetResult(new AdminLogsCardSnapshot { UserId = 7, Entries = [Entry(2), Entry(1)] });

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("msg-10", cut.Markup);
            Assert.Contains("msg-2", cut.Markup);
        }, _timeout);
        response.SetResult(Json(Entry(2), Entry(1)));
        await coordinator.NextCompletion();
        Assert.Contains("msg-10", cut.Markup);
    }

    [Fact]
    public async Task LiveEntryDuringReconnectRead_IsNotDroppedByLateResult()
    {
        var snapshots = SnapshotsWith(7, Entry(2), Entry(1));
        var initial = Task.FromResult(Json(Entry(2), Entry(1)));
        var reconnect = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(initial, reconnect.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminLogsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Requests), _timeout);

        var refresh = cut.InvokeAsync(() => cut.Instance.RefreshAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Requests), _timeout);
        await cut.InvokeAsync(() => cut.Instance.ApplyLiveBatchAsync([Dto(9)]));
        reconnect.SetResult(Json(Entry(3), Entry(2), Entry(1)));
        await refresh;

        Assert.Contains("msg-9", cut.Markup);
        Assert.Contains("msg-3", cut.Markup);
        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.Is<AdminLogsCardSnapshot>(
            snapshot => !snapshot.Entries.Any(e => e.Id == 9) && snapshot.Entries.Any(e => e.Id == 3))), Times.Never);
    }

    [Fact]
    public async Task RestFailure_WithSnapshot_KeepsSnapshotWithoutError()
    {
        var snapshots = SnapshotsWith(7, Entry(1));
        var coordinator = new SignalingCoordinator(snapshots.Object);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler, coordinator);
        var cut = context.Render<AdminLogsCard>();
        cut.WaitForAssertion(() => Assert.Contains("msg-1", cut.Markup), _timeout);

        response.SetResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await coordinator.NextCompletion();

        Assert.Contains("msg-1", cut.Markup);
        Assert.DoesNotContain("Failed to load logs", cut.Markup);
        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AdminLogsCardSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RestFailure_WithoutSnapshot_ShowsError()
    {
        var snapshots = new Mock<ISnapshotService>();
        using var handler = new StubHandler(Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var context = Context(snapshots, handler);

        var cut = context.Render<AdminLogsCard>();

        cut.WaitForAssertion(() => Assert.Contains("Failed to load logs", cut.Markup), _timeout);
    }

    [Fact]
    public async Task OtherUsersSnapshot_IsNotPainted_AndKeysArePerUser()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(s => s.GetAsync<AdminLogsCardSnapshot>("admin-logs:7"))
            .ReturnsAsync(new AdminLogsCardSnapshot { UserId = 99, Entries = [Entry(1)] });
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler);

        var cut = context.Render<AdminLogsCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Requests), _timeout);

        Assert.DoesNotContain("msg-1", cut.Markup);
        snapshots.Verify(s => s.GetAsync<AdminLogsCardSnapshot>("admin-logs:7"), Times.Once);
        snapshots.Verify(s => s.GetAsync<AdminLogsCardSnapshot>("admin-logs:99"), Times.Never);
        response.SetResult(Json(Entry(2)));
    }

    [Fact]
    public async Task LiveBatch_StorageWriteFailure_IsNonFatal()
    {
        var snapshots = SnapshotsWith(7, Entry(1));
        snapshots.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AdminLogsCardSnapshot>()))
            .ThrowsAsync(new InvalidOperationException("quota"));
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminLogsCard>();
        cut.WaitForAssertion(() => Assert.Contains("msg-1", cut.Markup), _timeout);

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveBatchAsync([Dto(8)]));

        cut.WaitForAssertion(() => Assert.Contains("msg-8", cut.Markup), _timeout);
        response.SetResult(Json(Entry(1)));
    }

    private static AdminLogEntryView Entry(int id) => new(id, _baseTime.AddMinutes(id), LogSeverity.Error, "Cat", $"msg-{id}");

    private static LogEntryDto Dto(int id) => new(id, _baseTime.AddMinutes(id), LogSeverity.Error, "Cat", $"msg-{id}", null, null, null);

    private static string Ids(AdminLogsCardSnapshot snapshot) => string.Join(",", snapshot.Entries.Select(e => e.Id));

    private static HttpResponseMessage Json(params AdminLogEntryView[] entries) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(entries.Select(e => Dto(e.Id)).ToArray()) };

    private static Mock<ISnapshotService> SnapshotsWith(int userId, params AdminLogEntryView[] entries)
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(s => s.GetAsync<AdminLogsCardSnapshot>($"admin-logs:{userId}"))
            .ReturnsAsync(new AdminLogsCardSnapshot { UserId = userId, Entries = [.. entries], FetchedAtUtc = DateTime.UtcNow.AddDays(-30) });
        return snapshots;
    }

    private static BunitContext Context(Mock<ISnapshotService> snapshots, HttpMessageHandler handler, SignalingCoordinator? coordinator = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new AdminLogsHttpClient(http));
        var login = new Mock<ILoginService>();
        login.Setup(l => l.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 7,
            UserName = "admin",
            Password = string.Empty,
            UserRole = UserRole.Admin,
        });
        context.Services.AddSingleton(login.Object);
        context.Services.AddSingleton(snapshots.Object);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(coordinator ?? new SignalingCoordinator(snapshots.Object));
        context.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<AdminLogsCard>>(NullLogger<AdminLogsCard>.Instance);
        return context;
    }

    private sealed class SignalingCoordinator(ISnapshotService snapshots) : ISnapshotRefreshCoordinator
    {
        private readonly SnapshotRefreshCoordinator _inner = new(snapshots, NullLogger<SnapshotRefreshCoordinator>.Instance);
        private readonly SemaphoreSlim _completions = new(0);

        public Task NextCompletion() => _completions.WaitAsync(_timeout, Xunit.TestContext.Current.CancellationToken);

        public async Task<SnapshotRefreshResult<TModel>> RunAsync<TSnapshot, TModel>(SnapshotRefreshRequest<TSnapshot, TModel> request)
            where TSnapshot : SnapshotBase where TModel : class
        {
            try { return await _inner.RunAsync(request); }
            finally { _completions.Release(); }
        }
    }

    /// <summary>Serves queued responses in order and counts requests.</summary>
    private sealed class StubHandler(params Task<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _requests;
        public int Requests => Volatile.Read(ref _requests);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref _requests) - 1;
            return await responses[Math.Min(index, responses.Length - 1)];
        }
    }
}