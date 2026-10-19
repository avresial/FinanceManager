using Microsoft.Extensions.Configuration;

namespace FinanceManager.Tests.Integration;

public sealed class OptionsProvider
{
    private readonly IConfigurationRoot _configuration;

    public OptionsProvider() => _configuration = GetConfigurationRoot();

    public T Get<T>(string sectionName) where T : class, new() => _configuration.GetSection(sectionName).Get<T>() ?? new T();

    private static IConfigurationRoot GetConfigurationRoot()
        => new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json", true)
            .AddEnvironmentVariables()
            .Build();
}