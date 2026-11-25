using FinanceManager.Api.Features.Labels.Services;
using FinanceManager.Application.Alerts.Services;
using FinanceManager.Application.Labels.Setter;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Api.Features.Labels.Services;

[Trait("Category", "Unit")]
public sealed class LabelSetterWorkerTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Worker_ScopesLabelingToTheRequestsUser_AndSkipsGuestAndZeroUsers()
    {
        var ai = new Mock<ILabelSetterAiService>();
        ai.Setup(service => service.AssignLabels(7, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, IReadOnlyCollection<long> ids, CancellationToken _) => ids.ToDictionary(id => id, _ => "Food"));
        var entries = new Mock<ITransactionEntryRepository<CashEntry>>();
        entries.Setup(repository => repository.AddLabels(7, It.IsAny<IEnumerable<(long, int)>>(), It.IsAny<CancellationToken>())).ReturnsAsync(3);
        var alerts = new Mock<IFinancialAlertService>();
        var tracker = new Mock<ILabelSetterProgressTracker>();
        var finished = new SemaphoreSlim(0);
        tracker.Setup(t => t.CompleteJob()).Callback(() => finished.Release());
        var channel = new LabelSetterChannel(tracker.Object);
        await using var provider = Services(ai, entries, alerts, [new FinancialLabel { Id = 3, Name = "Food" }]);
        var worker = new LabelSetterBackgroundService(channel, provider.GetRequiredService<IServiceScopeFactory>(), tracker.Object, NullLogger<LabelSetterBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await channel.QueueEntries(-5, [1, 2], TestContext.Current.CancellationToken);
        await channel.QueueEntries(0, [3], TestContext.Current.CancellationToken);
        await channel.QueueEntries(7, [10, 11, 12], TestContext.Current.CancellationToken);
        Assert.True(await finished.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        await worker.StopAsync(CancellationToken.None);

        ai.Verify(service => service.AssignLabels(It.Is<int>(userId => userId <= 0), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Never);
        ai.Verify(service => service.AssignLabels(7, It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 10, 11, 12 })), It.IsAny<CancellationToken>()), Times.Once);
        entries.Verify(repository => repository.AddLabels(7, It.Is<IEnumerable<(long entryId, int labelId)>>(a => a.Count() == 3), It.IsAny<CancellationToken>()), Times.Once);
        entries.Verify(repository => repository.AddLabels(It.Is<int>(userId => userId != 7), It.IsAny<IEnumerable<(long, int)>>(), It.IsAny<CancellationToken>()), Times.Never);
        tracker.Verify(t => t.StartJob(7, 3), Times.Once);
        tracker.Verify(t => t.StartJob(It.Is<int>(userId => userId <= 0), It.IsAny<int>()), Times.Never);
        alerts.Verify(service => service.EvaluateAlertsAsync(7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Worker_AdvancesProgressEvenWhenTheAiReturnsNoAssignments()
    {
        var ai = new Mock<ILabelSetterAiService>();
        ai.Setup(service => service.AssignLabels(7, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var entries = new Mock<ITransactionEntryRepository<CashEntry>>();
        var tracker = new Mock<ILabelSetterProgressTracker>();
        var finished = new SemaphoreSlim(0);
        tracker.Setup(t => t.CompleteJob()).Callback(() => finished.Release());
        var channel = new LabelSetterChannel(tracker.Object);
        await using var provider = Services(ai, entries, new Mock<IFinancialAlertService>(), []);
        var worker = new LabelSetterBackgroundService(channel, provider.GetRequiredService<IServiceScopeFactory>(), tracker.Object, NullLogger<LabelSetterBackgroundService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        // 25 entries are two AI batches of 20 and 5; each must be reported even though nothing was assigned.
        await channel.QueueEntries(7, [.. Enumerable.Range(1, 25).Select(id => (long)id)], TestContext.Current.CancellationToken);
        Assert.True(await finished.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        await worker.StopAsync(CancellationToken.None);

        tracker.Verify(t => t.ReportBatchCompleted(20), Times.Once);
        tracker.Verify(t => t.ReportBatchCompleted(5), Times.Once);
        entries.Verify(repository => repository.AddLabels(It.IsAny<int>(), It.IsAny<IEnumerable<(long, int)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartupScan_QueuesOneJobPerRealUser_IncludingUnlinkedEntries_AndSkipsDemoAndGuests()
    {
        const int demoUserId = 9;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var seed = new AppDbContext(options))
        {
            var label = new FinancialLabel { Name = "Food" };
            seed.FinancialLabels.Add(label);
            CashEntry Entry(int userId, int? accountId, string description, string? contractor = null, params FinancialLabel[] labels) =>
                new(0, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), -1m) { UserId = userId, CurrencyId = 1, Description = description, ContractorDetails = contractor, Labels = labels };
            seed.CurrencyEntries.AddRange(
                Entry(5, null, "unlinked"),
                Entry(5, 40, "linked"),
                Entry(5, null, "", "contractor only"),
                Entry(5, null, "", null),
                Entry(5, null, "already labelled", null, label),
                Entry(6, null, "other user"),
                Entry(demoUserId, null, "demo user"),
                Entry(-3, null, "guest sandbox"),
                Entry(0, null, "orphan"));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.GetUser("demo")).ReturnsAsync(new User { UserId = demoUserId, Login = "demo", CreationDate = DateTime.UtcNow });
        var queued = new Dictionary<int, long[]>();
        var done = new TaskCompletionSource();
        var channel = new Mock<ILabelSetterChannel>();
        channel.Setup(c => c.QueueEntries(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .Callback((int userId, IReadOnlyCollection<long> ids, CancellationToken _) =>
            {
                lock (queued) queued[userId] = [.. ids];
                if (queued.Count == 2) done.TrySetResult();
            })
            .Returns(ValueTask.CompletedTask);
        var services = new ServiceCollection()
            .AddScoped(_ => new AppDbContext(options))
            .AddSingleton(users.Object)
            .BuildServiceProvider();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DefaultUser:Login"] = "demo" }).Build();
        var startup = new LabelSetterStartupService(services.GetRequiredService<IServiceScopeFactory>(), channel.Object, configuration, NullLogger<LabelSetterStartupService>.Instance);

        await ((Microsoft.Extensions.Hosting.IHostedService)startup).StartAsync(CancellationToken.None);
        await done.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal([5, 6], queued.Keys.Order());
        Assert.Equal(3, queued[5].Length);
        Assert.Single(queued[6]);
        channel.Verify(c => c.QueueEntries(It.Is<int>(userId => userId == demoUserId || userId <= 0), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AiService_NeverReadsEntriesWithoutAUser_AndAsksTheRepositoryForTheUsersOwnEntries()
    {
        var entries = new Mock<ITransactionEntryRepository<CashEntry>>();
        entries.Setup(repository => repository.GetByIds(7, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var labels = new Mock<IFinancialLabelsRepository>();
        labels.Setup(repository => repository.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { new FinancialLabel { Id = 1, Name = "Food" } }.ToAsyncEnumerable());
        var service = CreateAiService(entries.Object, labels.Object);

        Assert.Empty(await service.AssignLabels(0, [1, 2], TestContext.Current.CancellationToken));
        Assert.Empty(await service.AssignLabels(7, [], TestContext.Current.CancellationToken));
        Assert.Empty(await service.AssignLabels(7, [1, 2], TestContext.Current.CancellationToken));

        entries.Verify(repository => repository.GetByIds(0, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Never);
        entries.Verify(repository => repository.GetByIds(7, It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 1, 2 })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AiService_NeverReturnsAnAssignmentForAnEntryTheUserDoesNotOwn()
    {
        var owned = new CashEntry(10, new DateTime(2026, 1, 1), -5m) { Description = "Lunch" };
        var entries = new Mock<ITransactionEntryRepository<CashEntry>>();
        // The repository filters by owner: entry 20 is requested but belongs to someone else.
        entries.Setup(repository => repository.GetByIds(7, It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>())).ReturnsAsync([owned]);
        var labels = new Mock<IFinancialLabelsRepository>();
        labels.Setup(repository => repository.GetLabels(It.IsAny<CancellationToken>())).Returns(new[] { new FinancialLabel { Id = 1, Name = "Food" } }.ToAsyncEnumerable());
        var chat = new Mock<Microsoft.Extensions.AI.IChatClient>();
        chat.Setup(client => client.GetResponseAsync(It.IsAny<IEnumerable<Microsoft.Extensions.AI.ChatMessage>>(), It.IsAny<Microsoft.Extensions.AI.ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Microsoft.Extensions.AI.ChatResponse(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.Assistant,
                """{"assignments":[{"entryId":10,"labelName":"Food"},{"entryId":20,"labelName":"Food"}]}""")));
        var service = new LabelSetterAiService(entries.Object, labels.Object, Mock.Of<ILabelSetterPromptProvider>(), new LabelAssignmentResponseParser(),
            new LabelAssignmentValidator(NullLogger<LabelAssignmentValidator>.Instance), chat.Object, NullLogger<LabelSetterAiService>.Instance);

        var result = await service.AssignLabels(7, [10, 20], TestContext.Current.CancellationToken);

        Assert.Equal(new Dictionary<long, string> { [10] = "Food" }, result);
    }

    private static ILabelSetterAiService CreateAiService(ITransactionEntryRepository<CashEntry> entries, IFinancialLabelsRepository labels) =>
        new LabelSetterAiService(entries, labels, Mock.Of<ILabelSetterPromptProvider>(), new LabelAssignmentResponseParser(),
            new LabelAssignmentValidator(NullLogger<LabelAssignmentValidator>.Instance), Mock.Of<Microsoft.Extensions.AI.IChatClient>(),
            NullLogger<LabelSetterAiService>.Instance);

    private static ServiceProvider Services(
        Mock<ILabelSetterAiService> ai,
        Mock<ITransactionEntryRepository<CashEntry>> entries,
        Mock<IFinancialAlertService> alerts,
        List<FinancialLabel> labels)
    {
        var labelRepository = new Mock<IFinancialLabelsRepository>();
        labelRepository.Setup(repository => repository.GetLabels(It.IsAny<CancellationToken>())).Returns(() => labels.ToAsyncEnumerable());
        return new ServiceCollection()
            .AddSingleton(ai.Object)
            .AddSingleton(entries.Object)
            .AddSingleton(labelRepository.Object)
            .AddSingleton(alerts.Object)
            .BuildServiceProvider();
    }
}