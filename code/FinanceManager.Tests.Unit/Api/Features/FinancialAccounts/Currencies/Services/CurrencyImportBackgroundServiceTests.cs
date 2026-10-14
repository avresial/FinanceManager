using FinanceManager.Api.Features.FinancialAccounts.Currencies.Hubs;
using FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;
using FinanceManager.Api.Features.Identity.Guest;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.FinancialAccounts.Currencies.Import;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Imports;
using FinanceManager.Domain.FinancialAccounts.Shared.Imports;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Api.Features.FinancialAccounts.Currencies.Services;

[Collection("Api")]
[Trait("Category", "Unit")]
public class CurrencyImportBackgroundServiceTests
{
    private const int _guestUserId = -7;

    [Fact]
    public async Task GuestJob_PinsTheJobScopeToTheGuestSandboxBeforeResolvingImportServices()
    {
        // The job runs on a background scope without an HttpContext. Repositories pick the guest's
        // per-session in-memory database from IGuestSessionAccessor at resolution time, so the
        // guest must be pinned before the import service (and its repositories) are created.
        var guestIdSeenWhenResolving = (int?)null;
        var importService = new Mock<ICurrencyAccountImportService>();
        importService
            .Setup(x => x.ImportEntries(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<CurrencyEntryImport>>(),
                It.IsAny<CancellationToken>(), It.IsAny<Func<IReadOnlyList<ImportConflict>, Task>?>(), It.IsAny<Func<int, int, int, Task>?>()))
            .ReturnsAsync(new ImportResult(10, 1, 0, [], []));

        var alerts = new Mock<IFinancialAlertService>();
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IGuestSessionAccessor, GuestSessionAccessor>();
        services.AddScoped(sp =>
        {
            guestIdSeenWhenResolving = sp.GetRequiredService<IGuestSessionAccessor>().GuestUserId;
            return importService.Object;
        });
        services.AddScoped(_ => alerts.Object);
        await using var provider = services.BuildServiceProvider();

        var channel = new CurrencyImportJobChannel();
        var store = new CurrencyImportJobStore();
        var jobId = store.CreateJob(_guestUserId, accountId: 10, totalEntries: 1);
        await channel.QueueJob(new CurrencyImportJobRequest(jobId, _guestUserId, 10,
            [new CurrencyEntryImportRecordDto(DateTime.UtcNow.Date, -12.5m)], _guestUserId), TestContext.Current.CancellationToken);

        using var service = new CurrencyImportBackgroundService(channel, store, provider.GetRequiredService<IServiceScopeFactory>(),
            CreateHub(), NullLogger<CurrencyImportBackgroundService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitForCompletion(store, jobId);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_guestUserId, guestIdSeenWhenResolving);
    }

    [Fact]
    public async Task RegularUserJob_DoesNotPinAGuestSandbox()
    {
        var guestIdSeenWhenResolving = (int?)-1;
        var importService = new Mock<ICurrencyAccountImportService>();
        importService
            .Setup(x => x.ImportEntries(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<CurrencyEntryImport>>(),
                It.IsAny<CancellationToken>(), It.IsAny<Func<IReadOnlyList<ImportConflict>, Task>?>(), It.IsAny<Func<int, int, int, Task>?>()))
            .ReturnsAsync(new ImportResult(10, 1, 0, [], []));

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<IGuestSessionAccessor, GuestSessionAccessor>();
        services.AddScoped(sp =>
        {
            guestIdSeenWhenResolving = sp.GetRequiredService<IGuestSessionAccessor>().GuestUserId;
            return importService.Object;
        });
        services.AddScoped(_ => Mock.Of<IFinancialAlertService>());
        await using var provider = services.BuildServiceProvider();

        var channel = new CurrencyImportJobChannel();
        var store = new CurrencyImportJobStore();
        var jobId = store.CreateJob(5, accountId: 10, totalEntries: 1);
        await channel.QueueJob(new CurrencyImportJobRequest(jobId, 5, 10,
            [new CurrencyEntryImportRecordDto(DateTime.UtcNow.Date, -12.5m)]), TestContext.Current.CancellationToken);

        using var service = new CurrencyImportBackgroundService(channel, store, provider.GetRequiredService<IServiceScopeFactory>(),
            CreateHub(), NullLogger<CurrencyImportBackgroundService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await WaitForCompletion(store, jobId, userId: 5);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Null(guestIdSeenWhenResolving);
    }

    private static IHubContext<CurrencyImportHub> CreateHub()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(x => x.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<CurrencyImportHub>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);
        return hub.Object;
    }

    private static async Task WaitForCompletion(CurrencyImportJobStore store, Guid jobId, int userId = _guestUserId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            if (store.TryGetStatus(jobId, userId, out var status) && status is not null
                && status.Status is AsyncImportJobState.Completed or AsyncImportJobState.Failed)
                return;
            await Task.Delay(20, timeout.Token);
        }
    }
}