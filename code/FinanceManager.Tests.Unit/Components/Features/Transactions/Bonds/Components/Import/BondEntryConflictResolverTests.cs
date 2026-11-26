using Bunit;
using FinanceManager.Components.Features.Transactions.Bonds.Components.Import;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Imports;
using FinanceManager.Tests.Unit.Components.Features.Transactions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System.Text.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions.Bonds.Components.Import;

[Trait("Category", "Unit")]
public class BondEntryConflictResolverTests
{
    private static readonly DateTime _dayA = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _dayB = new(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ResubmitsEveryRowOfTheAcceptedKeys_AndNothingOfTheSkippedOnes()
    {
        var handler = RecordingHttpHandler.ForEntryForms(saved: new BondImportResult(1, 0, [], []));
        await using var context = CreateContext(handler);
        // The file holds the 5-unit row twice on day A and once more on day B; both days conflict with a different existing entry.
        BondEntryImportRecordDto[] rows =
        [
            new(_dayA, 5m, 1, "first"), new(_dayA, 5m, 1, "second"), new(_dayA, 7m, 2, "other bond"), new(_dayB, 5m, 1, "skipped day"),
        ];
        var cut = context.Render<BondEntryConflictResolver>(p => p
            .Add(x => x.ImportRows, rows)
            .Add(x => x.Conflicts, [Conflict(_dayA, 5m, 1), Conflict(_dayB, 5m, 1)]));

        await cut.InvokeAsync(() => cut.Instance.PickDay(_dayA, BondEntryConflictResolver.ResolveChoice.Imported));
        await cut.InvokeAsync(() => cut.Instance.PickDay(_dayB, BondEntryConflictResolver.ResolveChoice.Existing));
        await cut.InvokeAsync(cut.Instance.SubmitAsync);

        var request = Assert.Single(handler.Requests, r => r.PathAndQuery == "/api/transactions/bonds/import/resolve");
        var sent = JsonSerializer.Deserialize<BondEntryImportRecordDto[]>(request.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(["first", "second"], sent.Select(row => row.Description));
    }

    [Fact]
    public async Task WhenEveryDayIsSkipped_SendsNothing()
    {
        var handler = RecordingHttpHandler.ForEntryForms(saved: new BondImportResult(0, 0, [], []));
        await using var context = CreateContext(handler);
        var cut = context.Render<BondEntryConflictResolver>(p => p
            .Add(x => x.ImportRows, [new BondEntryImportRecordDto(_dayA, 5m, 1, "first")])
            .Add(x => x.Conflicts, [Conflict(_dayA, 5m, 1)]));

        await cut.InvokeAsync(() => cut.Instance.PickDay(_dayA, BondEntryConflictResolver.ResolveChoice.Existing));
        await cut.InvokeAsync(cut.Instance.SubmitAsync);

        Assert.DoesNotContain(handler.Requests, r => r.PathAndQuery.Contains("/import/resolve", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenTheServerReportsFailedRows_ShowsThemAndDoesNotCompleteTheImport()
    {
        var handler = RecordingHttpHandler.ForEntryForms(saved: new BondImportResult(0, 1, ["Posting date must be UTC."], []));
        await using var context = CreateContext(handler);
        var submitted = 0;
        var cut = context.Render<BondEntryConflictResolver>(p => p
            .Add(x => x.ImportRows, [new BondEntryImportRecordDto(_dayA, 5m, 1, "first")])
            .Add(x => x.OnSubmitted, () => submitted++)
            .Add(x => x.Conflicts, [Conflict(_dayA, 5m, 1)]));
        await cut.InvokeAsync(() => cut.Instance.PickDay(_dayA, BondEntryConflictResolver.ResolveChoice.Imported));

        await cut.InvokeAsync(cut.Instance.SubmitAsync);

        Assert.Equal(0, submitted);
        Assert.Contains("1 entries could not be imported: Posting date must be UTC.", cut.Find(".mud-alert").TextContent);
    }

    // An incoming row whose key (day, units, definition) differs from the existing entry of the day, so it is a real conflict.
    private static BondImportConflict Conflict(DateTime day, decimal units, int bondDetailsId) => new(
        new BondEntryImport(day, units, bondDetailsId, "incoming"),
        new BondEntry(null, 1, day, 0m, units + 100m, bondDetailsId), "Day already has entries.");

    private static BunitContext CreateContext(RecordingHttpHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddMudServices();
        var http = handler.CreateClient();
        context.Services.AddSingleton(new BondDetailsHttpClient(http));
        context.Services.AddSingleton(new BondTransactionsHttpClient(http));
        return context;
    }
}