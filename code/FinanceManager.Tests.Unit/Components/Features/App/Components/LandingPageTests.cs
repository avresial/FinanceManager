using Blazored.LocalStorage;
using Bunit;
using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Features.App.Components;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.App.Components;

[Trait("Category", "Unit")]
public class LandingPageTests
{
    [Fact]
    public async Task Renders_PricingHeadingAndDisabledGiftCodeButtonsWithResponsivePreviews()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLogging();
        var storage = new Mock<ILocalStorageService>();
        storage.Setup(service => service.ContainKeyAsync("isThisFirstVisit", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        context.Services.AddSingleton(storage.Object);
        context.Services.AddSingleton(Mock.Of<ILoginService>());
        context.Services.AddSingleton(new NewVisitorsHttpClient(new HttpClient(), NullLogger<NewVisitorsHttpClient>.Instance));

        var cut = context.Render<LandingPage>();

        Assert.Single(cut.FindAll("h3"), heading => heading.TextContent == "Pricing");
        var giftCodeButtons = cut.FindAll("button.landing-gift-code");
        Assert.Equal(2, giftCodeButtons.Count);
        Assert.All(giftCodeButtons, button =>
        {
            Assert.True(button.HasAttribute("disabled"));
            Assert.Equal("Available by gift code", button.TextContent.Trim());
        });
        Assert.Equal(6, cut.FindAll(".landing-preview img[alt]").Count);

        var pictures = cut.FindAll(".landing-preview picture");
        Assert.Equal(6, pictures.Count);
        Assert.All(pictures, picture =>
        {
            var mobileSource = picture.QuerySelector("source")!;
            var desktopImage = picture.QuerySelector("img")!;
            Assert.Equal("(max-width: 599.98px)", mobileSource.GetAttribute("media"));
            Assert.EndsWith("-mobile.jpg", mobileSource.GetAttribute("srcset"));
            Assert.EndsWith("-desktop.jpg", desktopImage.GetAttribute("src"));
        });
    }
}