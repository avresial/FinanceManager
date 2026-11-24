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
        // Plans are limited by transactions only; no plan promises an account count any more.
        Assert.Contains(cut.FindAll(".mud-list-item"), item => item.TextContent.Contains("transactions", StringComparison.OrdinalIgnoreCase));
        Assert.All(cut.FindAll(".mud-list-item"), item => Assert.DoesNotContain("account", item.TextContent, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, cut.FindAll(".landing-preview img[alt]").Count);
        // The old Accounts section is a Transactions section: no account screenshot or wording is left.
        Assert.Single(cut.FindAll("h3"), heading => heading.TextContent == "Transactions");
        Assert.DoesNotContain(cut.FindAll("h3"), heading => heading.TextContent.Contains("Accounts", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("-account-", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IMGS/landing/transactions-desktop.jpg", cut.Markup);
        Assert.Contains("IMGS/landing/transactions-add-desktop.jpg", cut.Markup);

        var pictures = cut.FindAll(".landing-preview picture");
        Assert.Equal(4, pictures.Count);
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