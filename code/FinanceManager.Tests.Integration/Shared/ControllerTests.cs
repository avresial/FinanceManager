using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Xunit;

namespace FinanceManager.Tests.Integration.Shared;

public abstract class ControllerTests : IClassFixture<OptionsProvider>, IDisposable
{
    private static readonly object _appLock = new();
    private static readonly Dictionary<string, FinanceManagerApiTestApp> _apps = [];

    private readonly FinanceManagerApiTestApp _app;
    private readonly JwtTokenGenerator? _jwtTokenGenerator;
    protected HttpClient Client { get; }

    protected LoginResponseModel? Authorize(string userName, int userId, UserRole role)
    {
        if (_jwtTokenGenerator is null) return null;
        var jwt = _jwtTokenGenerator.GenerateToken(userName, userId, role);

        if (jwt is null) return null;

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.AccessToken);

        return jwt;
    }

    protected void ClearAuthorization()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    public ControllerTests(OptionsProvider optionsProvider, string environmentName = "test")
    {
        var authOptions = optionsProvider.Get<JwtAuthOptions>("JwtConfig");
        _jwtTokenGenerator = new JwtTokenGenerator(new OptionsWrapper<JwtAuthOptions>(authOptions));

        if (!ReuseApplicationHost)
        {
            _app = new FinanceManagerApiTestApp(ConfigureServices, environmentName);
            Client = _app.CreateClient();
            Client.BaseAddress = new Uri("http://localhost/");
            return;
        }

        var appKey = $"{GetType().AssemblyQualifiedName}|{environmentName}";
        var created = false;
        lock (_appLock)
        {
            if (!_apps.TryGetValue(appKey, out _app!))
            {
                _app = new FinanceManagerApiTestApp(services =>
                {
                    using (TestDatabase.UseDatabase(appKey))
                        ConfigureServices(services);
                }, environmentName);

                _apps.Add(appKey, _app);
                created = true;
            }
        }

        if (!created)
        {
            // The host already built its provider, so nothing registered here can reach it — this call is made
            // purely for the side effects inside ConfigureServices that a reused host still needs per test
            // instance: resetting shared mocks, and re-acquiring the fixture's TestDatabase. The scope makes that
            // TestDatabase resolve to the very context the host was built with, so the discarded registrations
            // would be identical to the live ones anyway. Cross-test data is prevented by the reset below.
            using (TestDatabase.UseDatabase(appKey))
                ConfigureServices(new ServiceCollection());
        }

        using (var scope = _app.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            database.ChangeTracker.Clear();
            database.Database.EnsureDeleted();
            database.Database.EnsureCreated();
        }

        Client = _app.CreateClient();
        Client.BaseAddress = new Uri("http://localhost/");
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    protected virtual bool ReuseApplicationHost => true;

    public virtual void Dispose()
    {
        ClearAuthorization();
        Client.Dispose();
        if (!ReuseApplicationHost)
            _app.Dispose();
    }
}