using Blazored.LocalStorage;
using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards.Assets;

[Trait("Category", "Unit")]
public class TimeWeightedReturnCardTests
{
    [Fact]
    public async Task RangeChange_IgnoresSlowerPreviousResponse()
    {
        var firstStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstEnd = firstStart.AddDays(6);
        var secondStart = firstStart.AddDays(7);
        var secondEnd = secondStart.AddDays(6);
        var handler = new QueuedAssetsHandler();
        await using var context = CreateContext(handler);

        var cut = context.Render<TimeWeightedReturnCard>(parameters => parameters
            .Add(component => component.StartDateTime, firstStart)
            .Add(component => component.EndDateTime, firstEnd));
        Assert.Equal(1, handler.TimeWeightedReturnRequestCount);

        cut.Render(parameters => parameters
            .Add(component => component.StartDateTime, secondStart)
            .Add(component => component.EndDateTime, secondEnd));
        Assert.Equal(2, handler.TimeWeightedReturnRequestCount);

        handler.Complete(1, new(0.2m, TimeWeightedReturnStatus.Available, secondStart, secondEnd));
        cut.WaitForAssertion(() => Assert.Contains("20.00%", cut.Markup));

        var renderCount = cut.RenderCount;
        handler.Complete(0, new(0.1m, TimeWeightedReturnStatus.Available, firstStart, firstEnd));
        cut.WaitForState(() => cut.RenderCount > renderCount);

        Assert.Contains("20.00%", cut.Markup);
        Assert.DoesNotContain("10.00%", cut.Markup);
    }

    private static BunitContext CreateContext(HttpMessageHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();

        var settings = new Mock<ISettingsService>();
        settings.Setup(service => service.GetCurrencyAsync()).ReturnsAsync(DefaultCurrency.PLN);
        context.Services.AddSingleton(settings.Object);

        var login = new Mock<ILoginService>();
        login.Setup(service => service.GetLoggedUser()).ReturnsAsync(new UserSession
        {
            UserId = 1,
            UserName = "tester",
            Password = string.Empty,
            UserRole = UserRole.User,
        });
        context.Services.AddSingleton(login.Object);

        context.Services.AddSingleton(new AssetsPageCardsCacheService(
            Mock.Of<ILocalStorageService>(),
            new MemoryCache(new MemoryCacheOptions()),
            new AssetsHttpClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }),
            NullLogger<AssetsPageCardsCacheService>.Instance));

        return context;
    }

    private sealed class QueuedAssetsHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource<HttpResponseMessage>[] _timeWeightedReturnResponses =
        [
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
        ];
        private int _timeWeightedReturnRequestCount;

        public int TimeWeightedReturnRequestCount => Volatile.Read(ref _timeWeightedReturnRequestCount);

        public void Complete(int index, TimeWeightedReturnResult result) =>
            _timeWeightedReturnResponses[index].SetResult(Response(result));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("GetTimeWeightedReturn", StringComparison.Ordinal))
            {
                var index = Interlocked.Increment(ref _timeWeightedReturnRequestCount) - 1;
                return _timeWeightedReturnResponses[index].Task;
            }

            if (path.Contains("GetMoneyWeightedReturn", StringComparison.Ordinal))
                return Task.FromResult(Response(new MoneyWeightedReturnResult(
                    null,
                    MoneyWeightedReturnStatus.InsufficientData,
                    DateTime.MinValue,
                    DateTime.MinValue)));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            });
        }

        private static HttpResponseMessage Response<T>(T value) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value),
        };
    }
}