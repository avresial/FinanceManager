using FinanceManager.Api.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Labels.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class LabelSetterProgressControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        var trackerMock = new Mock<ILabelSetterProgressTracker>();
        trackerMock.Setup(x => x.GetSnapshot())
            .Returns(new LabelSetterProgressSnapshot(new LabelSetterJobProgress(10, 20, 30, 12, DateTime.UtcNow), 2));
        services.AddSingleton(trackerMock.Object);
    }

    [Fact]
    public async Task Get_AsAdmin_ReturnsProgressSnapshot()
    {
        Authorize("admin", 1, UserRole.Admin);

        var response = await Client.GetAsync("api/LabelSetterProgress", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"queuedJobsCount\":2", content, StringComparison.OrdinalIgnoreCase);
    }
}