using FinanceManager.Domain.Entities.Logging;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class AdminLogsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        var repositoryMock = new Mock<ILogEntryRepository>();
        repositoryMock.Setup(x => x.GetLatest(5, It.IsAny<IReadOnlyCollection<LogSeverity>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new LogEntry
                {
                    Id = 7,
                    TimestampUtc = DateTime.UtcNow,
                    Level = LogSeverity.Warning,
                    Category = "Test",
                    Message = "A warning"
                }
            ]);
        services.AddSingleton(repositoryMock.Object);
    }

    [Fact]
    public async Task GetLatest_AsAdmin_ReturnsLogEntries()
    {
        Authorize("admin", 1, UserRole.Admin);

        var response = await Client.GetAsync("api/admin/logs/latest", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("A warning", content, StringComparison.OrdinalIgnoreCase);
    }
}