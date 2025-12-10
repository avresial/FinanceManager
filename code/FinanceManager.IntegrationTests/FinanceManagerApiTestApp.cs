using FinanceManager.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FinanceManager.IntegrationTests;

internal sealed class FinanceManagerApiTestApp : WebApplicationFactory<Program>
{
    public HttpClient Client { get; }

    public FinanceManagerApiTestApp(Action<IServiceCollection>? services = null)
    {
        Client = WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(s =>
            {
                // Remove DatabaseInitializer hosted service from integration tests
                var databaseInitializerDescriptor = s.FirstOrDefault(d => d.ImplementationType == typeof(DatabaseInitializer));
                if (databaseInitializerDescriptor != null)
                    s.Remove(databaseInitializerDescriptor);

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