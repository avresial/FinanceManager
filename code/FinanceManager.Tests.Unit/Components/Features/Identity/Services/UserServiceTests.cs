using FinanceManager.Components.Features.Identity.HttpClients;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.Identity.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Unit.Components.Features.Identity.Services;

[Trait("Category", "Unit")]
public class UserServiceTests
{
    [Fact]
    public async Task GetUser_ConcurrentCallsForSameUser_ShareOneRequest()
    {
        var gate = new TaskCompletionSource();
        var handler = new RequestCountingHandler(UserFor, gate.Task);
        var service = CreateService(handler);

        var first = service.GetUser(7);
        var second = service.GetUser(7);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, handler.GetCount("/api/User/Get/7"));
        Assert.Equal(7, (await second)!.UserId);
    }

    [Fact]
    public async Task GetUser_SequentialCalls_FetchFreshData()
    {
        var handler = new RequestCountingHandler(UserFor);
        var service = CreateService(handler);

        await service.GetUser(7);
        await service.GetUser(7);

        // Only concurrent requests are shared; completed results are never cached.
        Assert.Equal(2, handler.GetCount("/api/User/Get/7"));
    }

    [Fact]
    public async Task GetUser_ConcurrentCallsForDifferentUsers_AreNotShared()
    {
        var gate = new TaskCompletionSource();
        var handler = new RequestCountingHandler(UserFor, gate.Task);
        var service = CreateService(handler);

        var first = service.GetUser(7);
        var second = service.GetUser(8);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(7, (await first)!.UserId);
        Assert.Equal(8, (await second)!.UserId);
    }

    private static object UserFor(string path) => new User
    {
        UserId = int.Parse(path[(path.LastIndexOf('/') + 1)..]),
        Login = "user",
        CreationDate = DateTime.UtcNow,
    };

    private static UserService CreateService(RequestCountingHandler handler) =>
        new(new UserHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }),
            NullLogger<UserService>.Instance);
}