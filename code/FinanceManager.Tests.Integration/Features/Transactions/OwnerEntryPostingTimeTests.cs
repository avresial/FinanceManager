using FinanceManager.Application.Identity.Users;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Bonds;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Transactions;

/// <summary>
/// The Transactions page sends the wall-clock time the user picked (no offset) and shows what comes back through
/// <c>ToLocalTime()</c>, exactly as the account pages do. The owner routes must therefore store and return that time
/// as submitted; stamping it UTC shifted a 01:00 entry to 02:00 for a user at UTC+1. The check only bites when the
/// machine's time zone is not UTC, which is the case for developers outside it.
/// </summary>
[Trait("Category", "Integration")]
public class OwnerEntryPostingTimeTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const int _owner = 1;
    private const int _bondId = 11;
    private static readonly DateTime _submitted = new(2026, 11, 23, 1, 0, 0, DateTimeKind.Unspecified);
    private TestDatabase? _database;

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _database = new TestDatabase();
        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null) services.Remove(descriptor);
        services.AddSingleton(_database.Context);

        Mock<IUserPlanVerifier> planVerifier = new();
        planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(planVerifier.Object);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
        _database = null;
        GC.SuppressFinalize(this);
    }

    private AppDbContext Db => _database!.Context;

    // What the row shows: the stored UTC instant through ToLocalTime, which must read back as the time the user picked.
    private static void AssertAsSubmitted(DateTime actual, DateTime expected) => Assert.Equal(expected, actual.ToLocalTime());

    [Fact]
    public async Task CashEntry_KeepsTheSubmittedTime_OnCreateAndUpdate()
    {
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var created = await Client.PostAsJsonAsync("api/transactions/cash", new CreateCashEntryRequest(0, _submitted, -5m, "Coffee", null, null), ct);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct);
        AssertAsSubmitted(dto!.PostingDate, _submitted);
        Db.ChangeTracker.Clear();
        AssertAsSubmitted((await Db.CurrencyEntries.SingleAsync(ct)).PostingDate, _submitted);

        var later = _submitted.AddDays(1).AddHours(11);
        using var updated = await Client.PutAsJsonAsync($"api/transactions/cash/{dto.EntryId}", new UpdateCashEntryRequest(later, -6m, "Coffee", null, null), ct);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        AssertAsSubmitted((await updated.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct))!.PostingDate, later);
    }

    [Fact]
    public async Task BondEntry_KeepsTheSubmittedTime_OnCreateAndUpdate()
    {
        Db.Bonds.Add(new BondDetails("Bond", "Issuer", new DateOnly(2023, 1, 1), new DateOnly(2030, 1, 1),
            [new BondCalculationMethod { Id = _bondId, DateOperator = DateOperator.UntilDate, DateValue = "2030-01-01", Rate = 0.05m }])
        { Id = _bondId });
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var created = await Client.PostAsJsonAsync("api/transactions/bonds", new SaveBondEntryRequest(_bondId, _submitted, 10m, "Purchase", null), ct);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(ct);
        AssertAsSubmitted(dto!.PostingDate, _submitted);

        var later = _submitted.AddDays(1).AddHours(11);
        using var updated = await Client.PutAsJsonAsync($"api/transactions/bonds/{dto.EntryId}", new SaveBondEntryRequest(_bondId, later, 11m, "Purchase", null), ct);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        AssertAsSubmitted((await updated.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(ct))!.PostingDate, later);
    }
}