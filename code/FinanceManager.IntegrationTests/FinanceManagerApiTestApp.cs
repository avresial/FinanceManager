using FinanceManager.Api.Services;
using FinanceManager.Infrastructure.Services;
using FinanceManager.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceManager.IntegrationTests;

internal sealed class FinanceManagerApiTestApp : WebApplicationFactory<ApiEntryPoint>
{
    public HttpClient Client { get; }

    public FinanceManagerApiTestApp(Action<IServiceCollection>? services = null)
    {
        Client = WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(s =>
            {
                // Remove hosted services that access DB on startup to avoid
                // race conditions with the singleton in-memory test context.
                var databaseInitializerDescriptor = s.FirstOrDefault(d => d.ImplementationType == typeof(DatabaseInitializer));
                if (databaseInitializerDescriptor != null)
                    s.Remove(databaseInitializerDescriptor);

                var labelSetterDescriptor = s.FirstOrDefault(d => d.ImplementationType == typeof(LabelSetterStartupService));
                if (labelSetterDescriptor != null)
                    s.Remove(labelSetterDescriptor);

                services?.Invoke(s);
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    //["Key"] = "value",
                };
                config.AddInMemoryCollection(settings);
            });

            builder.UseEnvironment("test");
        }).CreateClient();
        Client.BaseAddress = new Uri("http://localhost/");
    }
}