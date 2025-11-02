using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;
[Collection("api")]
public abstract class ControllerTests : IClassFixture<OptionsProvider>
{
    //private readonly IAuthenticator _authenticator;
    protected HttpClient Client { get; }

    //protected JwtDto Authorize(Guid userId, string role)
    //{
    //    var jwt = _authenticator.CreateToken(userId, role);
    //    Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.AccessToken);

    //    return jwt;
    //}

    public ControllerTests(OptionsProvider optionsProvider)
    {
        //var authOptions = optionsProvider.Get<AuthOptions>("auth");
        //_authenticator = new Authenticator(new OptionsWrapper<AuthOptions>(authOptions), new Clock());
        var app = new FinanceManagerApiTestApp(ConfigureServices);
        Client = app.Client;
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }
}