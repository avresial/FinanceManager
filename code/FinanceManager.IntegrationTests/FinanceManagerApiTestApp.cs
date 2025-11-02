using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.IntegrationTests;
internal sealed class FinanceManagerApiTestApp : WebApplicationFactory<Program>
{
    public HttpClient Client { get; }

    public FinanceManagerApiTestApp(Action<IServiceCollection> services = null)
    {
        Client = WithWebHostBuilder(builder =>
        {
            if (services is not null)
            {
                builder.ConfigureServices(services);
            }

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
    }
}