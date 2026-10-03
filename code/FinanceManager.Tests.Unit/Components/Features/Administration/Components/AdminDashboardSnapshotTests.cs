using ApexCharts;
using Bunit;
using FinanceManager.Application.Identity;
using FinanceManager.Components.Features.Administration.Components;
using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Features.Administration.Models;
using FinanceManager.Components.Features.Administration.Services;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Shared.Charting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Administration.Components;

public sealed class AdminDashboardSnapshotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountsCountSnapshot_RefreshesIndependentlyOfChartAndMetrics(bool failedRefresh)
    {
        var snapshots = Snapshots("7", 3);
        snapshots.Setup(service => service.GetAsync<AdminAccountsCountSnapshot>("admin-accounts-count:7"))
            .ReturnsAsync(new AdminAccountsCountSnapshot { UserId = 7, Count = 42 });
        var chart = new TaskCompletionSource<HttpResponseMessage>();
        var metrics = new TaskCompletionSource<HttpResponseMessage>();
        var accounts = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(chart.Task, metrics.Task, accounts.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("42", cut.Markup);
            Assert.Equal(3, Series(cut).Single().Value);
        });
        Assert.Equal(1, handler.AccountsRequests);
        accounts.SetResult(failedRefresh
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Json(43));
        await context.Services.GetRequiredService<TrackingCoordinator>().AccountsCompleted.Task
            .WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(failedRefresh ? "42" : "43", cut.Markup);
            snapshots.Verify(service => service.SetAsync("admin-accounts-count:7",
                It.Is<AdminAccountsCountSnapshot>(snapshot => snapshot.Count == 43)),
                failedRefresh ? Times.Never() : Times.Once());
        });
        chart.SetResult(Json(Entries(9)));
        metrics.SetResult(Json(1));
        cut.WaitForAssertion(() => Assert.Equal(9, Series(cut).Single().Value));
        Assert.Equal(1, handler.AccountsRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewVisitorsTodaySnapshot_RefreshesIndependentlyOfChartAndMetrics(bool failedRefresh)
    {
        var snapshots = Snapshots("7", 3);
        snapshots.Setup(service => service.GetAsync<AdminNewVisitorsTodaySnapshot>("admin-new-visitors-today:7"))
            .ReturnsAsync(new AdminNewVisitorsTodaySnapshot { UserId = 7, Day = DateTime.UtcNow.Date, Count = 42 });
        var chart = new TaskCompletionSource<HttpResponseMessage>();
        var metrics = new TaskCompletionSource<HttpResponseMessage>();
        var newVisitors = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(chart.Task, metrics.Task, newVisitors: newVisitors.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("42", cut.Markup);
            Assert.Equal(3, Series(cut).Single().Value);
        });
        Assert.Equal(1, handler.NewVisitorsRequests);
        newVisitors.SetResult(failedRefresh
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Json(43));
        await context.Services.GetRequiredService<TrackingCoordinator>().NewVisitorsCompleted.Task
            .WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(failedRefresh ? "42" : "43", cut.Markup);
            snapshots.Verify(service => service.SetAsync("admin-new-visitors-today:7",
                It.Is<AdminNewVisitorsTodaySnapshot>(snapshot => snapshot.Count == 43)),
                failedRefresh ? Times.Never() : Times.Once());
        });
        chart.SetResult(Json(Entries(9)));
        metrics.SetResult(Json(1));
        cut.WaitForAssertion(() => Assert.Equal(9, Series(cut).Single().Value));
        Assert.Equal(1, handler.NewVisitorsRequests);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(9, true)]
    public async Task SnapshotPaintsBeforeMetricsAndRefresh_OnlyChangedContentWrites(int freshValue, bool changed)
    {
        var snapshots = Snapshots("7", 3);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        var metrics = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(response.Task, metrics.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(3, Series(cut).Single().Value));
        Assert.Equal(1, handler.ChartRequests);
        var initialChart = cut.FindComponent<ApexChart<ChartEntryModel>>().Instance;
        response.SetResult(Json(Entries(freshValue)));
        await context.Services.GetRequiredService<TrackingCoordinator>().Completed.Task.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        snapshots.Verify(service => service.SetAsync("new-users-chart-7",
            It.IsAny<NewUsersSnapshot>()), changed ? Times.Once() : Times.Never());
        if (changed)
            cut.WaitForAssertion(() => Assert.Equal(9, Series(cut).Single().Value));
        else
            Assert.Same(initialChart, cut.FindComponent<ApexChart<ChartEntryModel>>().Instance);
        metrics.SetResult(Json(1));
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("null")]
    [InlineData("malformed")]
    public async Task RefreshFailure_KeepsPaintedSnapshot(string mode)
    {
        var snapshots = Snapshots("7", 3);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(3, Series(cut).Single().Value));
        response.SetResult(mode switch
        {
            "failure" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "null" => Json((List<ChartEntryModel>?)null),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid JSON") },
        });
        await context.Services.GetRequiredService<TrackingCoordinator>().Completed.Task.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Equal(3, Series(cut).Single().Value));
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<NewUsersSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task SuccessfulEmptySeries_ReplacesSnapshot()
    {
        var snapshots = Snapshots("7", 3);
        using var handler = new ChartHandler(Task.FromResult(Json(new List<ChartEntryModel>())));
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Empty(Series(cut)));
        snapshots.Verify(service => service.SetAsync("new-users-chart-7",
            It.Is<NewUsersSnapshot>(snapshot => snapshot.Entries.Count == 0)), Times.Once);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("8")]
    public async Task KeyIsScopedToAuthenticatedAdmin(string userId)
    {
        var snapshots = Snapshots(userId, 3);
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(9))));
        await using var context = Context(snapshots, handler, userId);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(9, Series(cut).Single().Value));
        snapshots.Verify(service => service.GetAsync<NewUsersSnapshot>($"new-users-chart-{userId}"), Times.Once);
        snapshots.Verify(service => service.SetAsync($"new-users-chart-{userId}", It.IsAny<NewUsersSnapshot>()), Times.Once);
    }

    [Fact]
    public async Task StorageReadAndWriteFailures_DoNotBlockFreshChart()
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<NewUsersSnapshot>(It.IsAny<string>())).ThrowsAsync(new IOException("read"));
        snapshots.Setup(service => service.SetAsync(It.IsAny<string>(), It.IsAny<NewUsersSnapshot>())).ThrowsAsync(new IOException("write"));
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(9))));
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(9, Series(cut).Single().Value));
    }

    [Fact]
    public async Task ChartsOwnSeparateOptionsAndIdentifiers()
    {
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(9))));
        await using var context = Context(new Mock<ISnapshotService>(), handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindComponents<ApexChart<ChartEntryModel>>().Count));
        var charts = cut.FindComponents<ApexChart<ChartEntryModel>>();
        Assert.NotSame(charts[0].Instance.Options, charts[1].Instance.Options);
        Assert.NotEqual(charts[0].Instance.Options.Chart.Id, charts[1].Instance.Options.Chart.Id);
    }

    [Fact]
    public async Task InitialHttpFailure_IsReported()
    {
        using var handler = new ChartHandler(Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var context = Context(new Mock<ISnapshotService>(), handler);
        Assert.Throws<HttpRequestException>(() => context.Render<AdminDashboard>());
    }

    [Theory]
    [InlineData(false, "7")]
    [InlineData(true, "")]
    [InlineData(true, "invalid")]
    public async Task MissingAdminContext_DoesNotReadSnapshot(bool admin, string userId)
    {
        var snapshots = new Mock<ISnapshotService>();
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(9))));
        await using var context = Context(snapshots, handler, userId, admin);
        Assert.Throws<InvalidOperationException>(() => context.Render<AdminDashboard>());
        snapshots.Verify(service => service.GetAsync<NewUsersSnapshot>(It.IsAny<string>()), Times.Never);
        Assert.Equal(0, handler.ChartRequests);
        snapshots.Verify(service => service.GetAsync<AdminAccountsCountSnapshot>(It.IsAny<string>()), Times.Never);
        snapshots.Verify(service => service.GetAsync<AdminNewVisitorsTodaySnapshot>(It.IsAny<string>()), Times.Never);
        snapshots.Verify(service => service.GetAsync<AdminUsersCountSnapshot>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SupersededRefresh_DoesNotReplaceChartOrStorage()
    {
        var snapshots = Snapshots("7", 3);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(3, Series(cut).Single().Value));
        var coordinator = context.Services.GetRequiredService<TrackingCoordinator>();
        Assert.NotNull(coordinator.Gate);
        coordinator.Gate.Claim();
        response.SetResult(Json(Entries(9)));
        await coordinator.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(3, Series(cut).Single().Value);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<NewUsersSnapshot>()), Times.Never);
    }

    private static readonly TimeSpan _dailyActiveTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task DailyActiveUsers_PaintsSnapshotBeforeFetchCompletes_AndAlwaysFetches()
    {
        var snapshots = DailyActiveSnapshots("7", 3);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(1))), dailyActive: response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(3, DailyActiveSeries(cut).Single().Value), _dailyActiveTimeout);
        Assert.Equal(1, handler.DailyActiveRequests);
        response.SetResult(Json(Entries(3)));
    }

    [Fact]
    public async Task DailyActiveUsers_ChangedData_RepaintsAndWrites()
    {
        var snapshots = DailyActiveSnapshots("7", 3);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(1))), dailyActive: response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(3, DailyActiveSeries(cut).Single().Value), _dailyActiveTimeout);
        response.SetResult(Json(Entries(9)));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(9, DailyActiveSeries(cut).Single().Value);
            snapshots.Verify(service => service.SetAsync("daily-active-users-chart-7",
                It.Is<DailyActiveUsersSnapshot>(snapshot => snapshot.Entries.Single().Value == 9)), Times.Once);
        }, _dailyActiveTimeout);
    }

    [Fact]
    public async Task DailyActiveUsers_UnchangedData_DoesNotRepaintOrWrite()
    {
        var snapshots = DailyActiveSnapshots("7", 3);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(1))), dailyActive: response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(3, DailyActiveSeries(cut).Single().Value), _dailyActiveTimeout);
        var initialChart = DailyActiveChart(cut);
        var coordinator = context.Services.GetRequiredService<TrackingCoordinator>();
        response.SetResult(Json(Entries(3)));
        await coordinator.DailyActiveCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.Same(initialChart, DailyActiveChart(cut));
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<DailyActiveUsersSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("null")]
    [InlineData("malformed")]
    public async Task DailyActiveUsers_RefreshFailure_KeepsPaintedSnapshot(string mode)
    {
        var snapshots = DailyActiveSnapshots("7", 3);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(1))), dailyActive: response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(3, DailyActiveSeries(cut).Single().Value), _dailyActiveTimeout);
        response.SetResult(mode switch
        {
            "failure" => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            "null" => Json((List<ChartEntryModel>?)null),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid JSON") },
        });
        await context.Services.GetRequiredService<TrackingCoordinator>().DailyActiveCompleted.Task
            .WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Equal(3, DailyActiveSeries(cut).Single().Value), _dailyActiveTimeout);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<DailyActiveUsersSnapshot>()), Times.Never);
    }

    [Fact]
    public async Task DailyActiveUsers_FailureWithoutSnapshot_DoesNotFailDashboard()
    {
        var snapshots = new Mock<ISnapshotService>();
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(9))),
            dailyActive: Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(9, Series(cut).Single().Value), _dailyActiveTimeout);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<DailyActiveUsersSnapshot>()), Times.Never);
    }

    [Theory]
    [InlineData("7", "8")]
    [InlineData("8", "7")]
    public async Task DailyActiveUsers_KeyIsScopedToAuthenticatedAdmin(string userId, string otherUserId)
    {
        var snapshots = DailyActiveSnapshots(userId, 3);
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(1))), dailyActive: Task.FromResult(Json(Entries(9))));
        await using var context = Context(snapshots, handler, userId);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(9, DailyActiveSeries(cut).Single().Value), _dailyActiveTimeout);
        snapshots.Verify(service => service.GetAsync<DailyActiveUsersSnapshot>($"daily-active-users-chart-{userId}"), Times.Once);
        snapshots.Verify(service => service.GetAsync<DailyActiveUsersSnapshot>($"daily-active-users-chart-{otherUserId}"), Times.Never);
        snapshots.Verify(service => service.SetAsync($"daily-active-users-chart-{userId}", It.IsAny<DailyActiveUsersSnapshot>()), Times.Once);
    }

    [Fact]
    public async Task DailyActiveUsers_SupersededRefresh_DoesNotReplaceChartOrStorage()
    {
        var snapshots = DailyActiveSnapshots("7", 3);
        var response = new TaskCompletionSource<HttpResponseMessage>();
        using var handler = new ChartHandler(Task.FromResult(Json(Entries(1))), dailyActive: response.Task);
        await using var context = Context(snapshots, handler);
        var cut = context.Render<AdminDashboard>();
        cut.WaitForAssertion(() => Assert.Equal(3, DailyActiveSeries(cut).Single().Value), _dailyActiveTimeout);
        var coordinator = context.Services.GetRequiredService<TrackingCoordinator>();
        Assert.NotNull(coordinator.DailyActiveGate);
        coordinator.DailyActiveGate.Claim();
        response.SetResult(Json(Entries(9)));
        await coordinator.DailyActiveCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(3, DailyActiveSeries(cut).Single().Value);
        snapshots.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<DailyActiveUsersSnapshot>()), Times.Never);
    }

    private static List<ChartEntryModel> Entries(int value) => [new(new DateTime(2026, 10, 1), value)];
    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private static IEnumerable<ChartEntryModel> Series(IRenderedComponent<AdminDashboard> cut) =>
        cut.FindComponents<ApexPointSeries<ChartEntryModel>>().Last().Instance.Items;

    private static IEnumerable<ChartEntryModel> DailyActiveSeries(IRenderedComponent<AdminDashboard> cut) =>
        cut.FindComponents<ApexPointSeries<ChartEntryModel>>().First().Instance.Items;

    private static ApexChart<ChartEntryModel> DailyActiveChart(IRenderedComponent<AdminDashboard> cut) =>
        cut.FindComponents<ApexChart<ChartEntryModel>>().First().Instance;

    private static Mock<ISnapshotService> DailyActiveSnapshots(string userId, int value)
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<DailyActiveUsersSnapshot>($"daily-active-users-chart-{userId}"))
            .ReturnsAsync(new DailyActiveUsersSnapshot { Entries = Entries(value), FetchedAtUtc = DateTime.UtcNow.AddDays(-30) });
        return snapshots;
    }

    private static Mock<ISnapshotService> Snapshots(string userId, int value)
    {
        var snapshots = new Mock<ISnapshotService>();
        snapshots.Setup(service => service.GetAsync<NewUsersSnapshot>($"new-users-chart-{userId}"))
            .ReturnsAsync(new NewUsersSnapshot { Entries = Entries(value), FetchedAtUtc = DateTime.UtcNow.AddDays(-30) });
        return snapshots;
    }

    private static BunitContext Context(Mock<ISnapshotService> snapshots, HttpMessageHandler handler,
        string userId = "7", bool admin = true)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.ComponentFactories.AddStub<LabelSetterProgressCard>();
        context.ComponentFactories.AddStub<AdminLogsCard>();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        context.Services.AddSingleton(new AdministrationUsersHttpClient(http));
        context.Services.AddSingleton(new NewVisitorsHttpClient(http, NullLogger<NewVisitorsHttpClient>.Instance));
        context.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<AdminDashboard>>(NullLogger<AdminDashboard>.Instance);
        var coordinator = new TrackingCoordinator(snapshots.Object);
        context.Services.AddSingleton(coordinator);
        context.Services.AddSingleton<ISnapshotRefreshCoordinator>(coordinator);
        context.Services.AddSingleton<AdminDashboardSnapshotStore>();
        var auth = new CustomAuthenticationStateProvider();
        auth.ChangeUser("local-admin", userId, admin ? UserRole.Admin : UserRole.User).GetAwaiter().GetResult();
        context.Services.AddSingleton<AuthenticationStateProvider>(auth);
        return context;
    }

    private sealed class TrackingCoordinator(ISnapshotService snapshots) : ISnapshotRefreshCoordinator
    {
        private readonly SnapshotRefreshCoordinator _inner = new(snapshots, NullLogger<SnapshotRefreshCoordinator>.Instance);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AccountsCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource NewVisitorsCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DailyActiveCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RefreshVersionGate? Gate { get; private set; }
        public RefreshVersionGate? DailyActiveGate { get; private set; }
        public async Task<SnapshotRefreshResult<TModel>> RunAsync<TSnapshot, TModel>(SnapshotRefreshRequest<TSnapshot, TModel> request)
            where TSnapshot : SnapshotBase where TModel : class
        {
            if (typeof(TSnapshot) == typeof(AdminNewVisitorsTodaySnapshot))
            {
                try { return await _inner.RunAsync(request); }
                finally { NewVisitorsCompleted.TrySetResult(); }
            }

            if (typeof(TSnapshot) == typeof(DailyActiveUsersSnapshot))
            {
                DailyActiveGate = request.Gate;
                try { return await _inner.RunAsync(request); }
                finally { DailyActiveCompleted.TrySetResult(); }
            }

            if (typeof(TSnapshot) != typeof(NewUsersSnapshot))
            {
                try { return await _inner.RunAsync(request); }
                finally { AccountsCompleted.TrySetResult(); }
            }

            Gate = request.Gate;
            try { return await _inner.RunAsync(request); }
            finally { Completed.TrySetResult(); }
        }
    }

    private sealed class ChartHandler(Task<HttpResponseMessage> chart, Task<HttpResponseMessage>? metrics = null,
        Task<HttpResponseMessage>? accounts = null, Task<HttpResponseMessage>? newVisitors = null,
        Task<HttpResponseMessage>? dailyActive = null) : HttpMessageHandler
    {
        public int ChartRequests { get; private set; }
        public int AccountsRequests { get; private set; }
        public int NewVisitorsRequests { get; private set; }
        public int DailyActiveRequests { get; private set; }
        public TaskCompletionSource Finished { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("GetNewUsersDaily"))
            {
                ChartRequests++;
                try { return await chart; }
                finally { Finished.TrySetResult(); }
            }
            if (request.RequestUri.AbsolutePath.EndsWith("GetUsersCount") && metrics is not null)
                return await metrics;
            if (request.RequestUri.AbsolutePath.EndsWith("GetAccountsCount"))
            {
                AccountsRequests++;
                if (accounts is not null)
                    return await accounts;
            }
            if (request.RequestUri.AbsolutePath.Contains("GetNewVisitor"))
            {
                NewVisitorsRequests++;
                if (newVisitors is not null)
                    return await newVisitors;
            }
            if (request.RequestUri.AbsolutePath.EndsWith("GetDailyActiveUsers"))
            {
                DailyActiveRequests++;
                return dailyActive is not null ? await dailyActive : Json(new List<ChartEntryModel>());
            }
            return Json(1);
        }
    }
}