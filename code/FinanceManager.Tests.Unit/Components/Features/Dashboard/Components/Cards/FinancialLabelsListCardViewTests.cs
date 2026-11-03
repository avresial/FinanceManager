using Bunit;
using FinanceManager.Components.Features.Dashboard.Components.Cards;
using MudBlazor.Services;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Components.Cards;

[Trait("Category", "Unit")]
public class FinancialLabelsListCardViewTests
{
    [Theory]
    [InlineData("2026-10-01", "2026-10-31", "Net · 1–31 Oct 2026")]
    [InlineData("2026-09-01", "2026-10-31", "Net · 1 Sep – 31 Oct 2026")]
    [InlineData("2025-12-15", "2026-01-14", "Net · 15 Dec 2025 – 14 Jan 2026")]
    public async Task Subtitle_UsesTheSharedRangeFormat(string start, string end, string expected)
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        var cut = context.Render<FinancialLabelsListCardView>(p => p
            .Add(x => x.StartDateTime, DateTime.Parse(start))
            .Add(x => x.EndDateTime, DateTime.Parse(end)));

        Assert.Equal(expected, cut.Find(".fm-card-header-subtitle").TextContent.Trim());
    }
}