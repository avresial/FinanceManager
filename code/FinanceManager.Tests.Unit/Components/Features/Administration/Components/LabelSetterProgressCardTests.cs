using Bunit;
using FinanceManager.Components.Features.Administration.Components;
using FinanceManager.Components.Features.Labels.HttpClients;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Administration.Components;

public sealed class LabelSetterProgressCardTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task StoredSnapshot_PaintsBeforeRestResponse_AndRestAlwaysRuns()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3, queued: 2));
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler);

        var cut = context.Render<LabelSetterProgressCard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("3 / 10", cut.Markup);
            Assert.Contains("2 queued", cut.Markup);
        }, _timeout);
        Assert.Equal(1, handler.Requests);
        response.SetResult(Json(Progress(processed: 3, queued: 2)));
    }

    [Fact]
    public async Task ChangedRestResult_RepaintsAndWrites()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3));
        var coordinator = new SignalingCoordinator(snapshots.Object);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler, coordinator);
        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Contains("3 / 10", cut.Markup), _timeout);

        response.SetResult(Json(Progress(processed: 6)));
        await coordinator.NextCompletion();

        cut.WaitForAssertion(() => Assert.Contains("6 / 10", cut.Markup), _timeout);
        snapshots.Verify(s => s.SetAsync("admin-label-setter-progress:7", It.Is<LabelSetterProgressCardSnapshot>(
            snapshot => snapshot.UserId == 7 && snapshot.Progress!.CurrentJob!.ProcessedEntries == 6)), Times.Once);
    }

    [Fact]
    public async Task UnchangedRestResult_DoesNotWrite()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3));
        var coordinator = new SignalingCoordinator(snapshots.Object);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler, coordinator);
        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Contains("3 / 10", cut.Markup), _timeout);

        response.SetResult(Json(Progress(processed: 3)));
        await coordinator.NextCompletion();

        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<LabelSetterProgressCardSnapshot>()), Times.Never);
        Assert.Contains("3 / 10", cut.Markup);
    }

    [Fact]
    public async Task LiveEvent_UpdatesRenderAndWritesSnapshot()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3));
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Contains("3 / 10", cut.Markup), _timeout);

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveProgressAsync(Progress(processed: 8)));

        cut.WaitForAssertion(() => Assert.Contains("8 / 10", cut.Markup), _timeout);
        snapshots.Verify(s => s.SetAsync("admin-label-setter-progress:7", It.Is<LabelSetterProgressCardSnapshot>(
            snapshot => snapshot.Progress!.CurrentJob!.ProcessedEntries == 8)), Times.Once);
        response.SetResult(Json(Progress(processed: 3)));
    }

    [Fact]
    public async Task LiveEventDuringRestRead_WinsOverLateRestResult()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3));
        var coordinator = new SignalingCoordinator(snapshots.Object);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler, coordinator);
        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Contains("3 / 10", cut.Markup), _timeout);

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveProgressAsync(Progress(processed: 8)));
        response.SetResult(Json(Progress(processed: 5)));
        await coordinator.NextCompletion();

        Assert.Contains("8 / 10", cut.Markup);
        Assert.DoesNotContain("5 / 10", cut.Markup);
        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<LabelSetterProgressCardSnapshot>()), Times.Once);
        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.Is<LabelSetterProgressCardSnapshot>(
            snapshot => snapshot.Progress!.CurrentJob!.ProcessedEntries == 5)), Times.Never);
    }

    [Fact]
    public async Task LiveEventDuringReconnectRead_WinsOverLateRestResult()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3));
        var initial = Task.FromResult(Json(Progress(processed: 3)));
        var reconnect = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(initial, reconnect.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Requests), _timeout);

        var refresh = cut.InvokeAsync(() => cut.Instance.RefreshAsync());
        cut.WaitForAssertion(() => Assert.Equal(2, handler.Requests), _timeout);
        await cut.InvokeAsync(() => cut.Instance.ApplyLiveProgressAsync(Progress(processed: 9)));
        reconnect.SetResult(Json(Progress(processed: 4)));
        await refresh;

        Assert.Contains("9 / 10", cut.Markup);
        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.Is<LabelSetterProgressCardSnapshot>(
            snapshot => snapshot.Progress!.CurrentJob!.ProcessedEntries == 4)), Times.Never);
    }

    [Fact]
    public async Task RestFailure_WithSnapshot_KeepsSnapshotWithoutError()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3));
        var coordinator = new SignalingCoordinator(snapshots.Object);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler, coordinator);
        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Contains("3 / 10", cut.Markup), _timeout);

        response.SetResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await coordinator.NextCompletion();

        Assert.Contains("3 / 10", cut.Markup);
        Assert.DoesNotContain("Failed to load progress", cut.Markup);
        snapshots.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<LabelSetterProgressCardSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task RestFailure_WithoutSnapshot_ShowsError()
    {
        var snapshots = new Mock<ISnapshotService>();
        using var handler = new StubHandler(Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var context = Context(snapshots, handler);

        var cut = context.Render<LabelSetterProgressCard>();

        cut.WaitForAssertion(() => Assert.Contains("Failed to load progress", cut.Markup), _timeout);
    }

    [Fact]
    public async Task OtherUsersSnapshot_IsNotPainted_AndKeysArePerUser()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(s => s.GetAsync<LabelSetterProgressCardSnapshot>("admin-label-setter-progress:7"))
            .ReturnsAsync(new LabelSetterProgressCardSnapshot { UserId = 99, Progress = Progress(processed: 3) });
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler);

        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Requests), _timeout);

        Assert.DoesNotContain("3 / 10", cut.Markup);
        snapshots.Verify(s => s.GetAsync<LabelSetterProgressCardSnapshot>("admin-label-setter-progress:7"), Times.Once);
        snapshots.Verify(s => s.GetAsync<LabelSetterProgressCardSnapshot>("admin-label-setter-progress:99"), Times.Never);
        response.SetResult(Json(Progress(processed: 1)));
    }

    [Fact]
    public async Task LiveEvent_StorageWriteFailure_IsNonFatal()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3));
        snapshots.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<LabelSetterProgressCardSnapshot>()))
            .ThrowsAsync(new InvalidOperationException("quota"));
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new StubHandler(response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Contains("3 / 10", cut.Markup), _timeout);

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveProgressAsync(Progress(processed: 8)));

        cut.WaitForAssertion(() => Assert.Contains("8 / 10", cut.Markup), _timeout);
        response.SetResult(Json(Progress(processed: 3)));
    }

    [Fact]
    public async Task ReconnectRead_EqualToStorage_ReplacesLiveStateWhoseWriteFailed()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3));
        snapshots.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<LabelSetterProgressCardSnapshot>()))
            .ThrowsAsync(new InvalidOperationException("quota"));
        var initial = Task.FromResult(Json(Progress(processed: 3)));
        var reconnect = Task.FromResult(Json(Progress(processed: 3)));
        using var handler = new StubHandler(initial, reconnect);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Equal(1, handler.Requests), _timeout);

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveProgressAsync(Progress(processed: 8)));
        cut.WaitForAssertion(() => Assert.Contains("8 / 10", cut.Markup), _timeout);
        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        cut.WaitForAssertion(() => Assert.Contains("3 / 10", cut.Markup), _timeout);
        Assert.DoesNotContain("8 / 10", cut.Markup);
    }

    [Fact]
    public async Task InitialFailure_ThenSuccessfulIdleReconnectRead_ClearsLoadError()
    {
        var snapshots = new Mock<ISnapshotService>();
        using var handler = new StubHandler(
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            Task.FromResult(Json(new LabelSetterProgressSnapshot(null, 0))));
        await using var context = Context(snapshots, handler);
        var cut = context.Render<LabelSetterProgressCard>();
        cut.WaitForAssertion(() => Assert.Contains("Failed to load progress", cut.Markup), _timeout);

        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No active labelling job.", cut.Markup);
            Assert.DoesNotContain("Failed to load progress", cut.Markup);
        }, _timeout);
    }

    [Fact]
    public async Task LiveEvent_ClearsLiveUpdatesWarning()
    {
        var snapshots = SnapshotsWith(7, Progress(processed: 3));
        using var handler = new StubHandler(Task.FromResult(Json(Progress(processed: 3))));
        await using var context = Context(snapshots, handler);
        var cut = context.Render<LabelSetterProgressCard>();
        // No hub listens in tests, so the connection attempt fails and shows the warning.
        cut.WaitForAssertion(() => Assert.Contains("Live updates disabled", cut.Markup), _timeout);

        await cut.InvokeAsync(() => cut.Instance.ApplyLiveProgressAsync(Progress(processed: 8)));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("8 / 10", cut.Markup);
            Assert.DoesNotContain("Live updates disabled", cut.Markup);
        }, _timeout);
    }

    private static LabelSetterProgressSnapshot Progress(int processed, int queued = 0) =>
        new(new LabelSetterJobProgress(5, 7, 10, processed, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)), queued);

    private static HttpResponseMessage Json(LabelSetterProgressSnapshot value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private static Mock<ISnapshotService> SnapshotsWith(int userId, LabelSetterProgressSnapshot progress)
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(s => s.GetAsync<LabelSetterProgressCardSnapshot>($"admin-label-setter-progress:{userId}"))
            .ReturnsAsync(new LabelSetterProgressCardSnapshot { UserId = userId, Progress = progress, FetchedAtUtc = DateTime.UtcNow.AddDays(-30) });
        return snapshots;
    }

    private static BunitContext Context(Mock<ISnapshotService> snapshots, HttpMessageHandler handler, SignalingCoordinator? coordinator = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new LabelSetterProgressHttpClient(http));
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
        context.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<LabelSetterProgressCard>>(NullLogger<LabelSetterProgressCard>.Instance);
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