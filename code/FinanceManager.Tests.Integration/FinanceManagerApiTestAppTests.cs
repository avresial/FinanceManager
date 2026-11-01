using FinanceManager.Api.Features.Labels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FinanceManager.Tests.Integration;

public sealed class FinanceManagerApiTestAppTests
{
    [Fact]
    public void DefaultHost_DoesNotRunBackgroundLabelWorker()
    {
        using var app = new FinanceManagerApiTestApp();

        Assert.DoesNotContain(app.Services.GetServices<IHostedService>(),
            service => service is LabelSetterBackgroundService);
        Assert.NotNull(app.Services.GetRequiredService<ILabelSetterChannel>());
    }

    [Fact]
    public void Host_CanExplicitlyEnableBackgroundLabelWorker()
    {
        using var app = new FinanceManagerApiTestApp(services =>
            services.AddHostedService<LabelSetterBackgroundService>());

        Assert.Single(app.Services.GetServices<IHostedService>(),
            service => service is LabelSetterBackgroundService);
    }
}