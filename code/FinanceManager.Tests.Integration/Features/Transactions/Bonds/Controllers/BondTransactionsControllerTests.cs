using FinanceManager.Application.Identity.Users;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Bonds;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Shared;
using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Imports;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Transactions.Bonds.Controllers;

[Trait("Category", "Integration")]
public class BondTransactionsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const string _route = "api/transactions/bonds";
    private const int _owner = 1;
    private const int _other = 2;
    private const int _guest = -7;
    private const int _accountId = 50;
    private const int _bondA = 11;
    private const int _bondB = 12;
    private const int _missingBond = 99;
    private static readonly DateTime _day = new(2024, 6, 3, 0, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IUserPlanVerifier> _planVerifier = new();
    private TestDatabase? _database;

    protected override bool ReuseApplicationHost => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        _database = new TestDatabase();
        var descriptor = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor is not null) services.Remove(descriptor);
        services.AddSingleton(_database.Context);

        _planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(_planVerifier.Object);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
        _database = null;
        GC.SuppressFinalize(this);
    }

    private AppDbContext Db => _database!.Context;

    // Bond 11 is in the default currency (PLN); every other definition shares one USD instance.
    private static BondDetails Bond(int id, Currency? currency = null) => new($"Bond {id}", "Issuer", new DateOnly(2023, 1, 1), new DateOnly(2030, 1, 1),
        [new BondCalculationMethod { Id = id, DateOperator = DateOperator.UntilDate, DateValue = "2030-01-01", Rate = 0.05m }], currency)
    { Id = id };

    private void Seed()
    {
        Db.Bonds.AddRange(Bond(_bondA), Bond(_bondB, new Currency(1, "USD", "$")));
        Db.Accounts.Add(new FinancialAccountBaseDto { AccountId = _accountId, UserId = _owner, Name = "Bonds", AccountType = AccountType.Bond });
        Db.SaveChanges();
    }

    private async Task<long> SeedEntry(int userId, int? accountId, int bondId, DateTime date, decimal valueChange, decimal value = 0m, string description = "seed")
    {
        var entry = new BondEntry(accountId, 0, date, value, valueChange, bondId) { UserId = userId, Description = description };
        Db.BondEntries.Add(entry);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry.EntryId;
    }

    private async Task<int> SeedLabel(string name)
    {
        var label = new FinancialLabel { Name = name };
        Db.FinancialLabels.Add(label);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return label.Id;
    }

    private static SaveBondEntryRequest Save(int bond = _bondA, decimal valueChange = 100m, IReadOnlyList<int>? labels = null, string? description = "Purchase") =>
        new(bond, _day.AddHours(9), valueChange, description, labels);

    [Fact]
    public async Task Anonymous_IsUnauthorizedOnEveryRoute()
    {
        ClearAuthorization();
        var ct = TestContext.Current.CancellationToken;
        BondEntryImportRecordDto[] rows = [new(_day, 1m, _bondA)];
        var responses = new[]
        {
            await Client.PostAsJsonAsync(_route, Save(), ct),
            await Client.PutAsJsonAsync(_route + "/1", Save(), ct),
            await Client.DeleteAsync(_route + "/1", ct),
            await Client.PostAsJsonAsync(_route + "/import", rows, ct),
            await Client.PostAsJsonAsync(_route + "/import/resolve", rows, ct),
            await Client.GetAsync(_route + "/export?bondDetailsId=11&startDate=2024-01-01&endDate=2024-12-31", ct),
        };
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.Unauthorized, x.StatusCode));
    }

    [Fact]
    public async Task Create_StoresAnUnlinkedOwnerRowWithASignedDelta_AndReturnsThePersistedRow()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        var label = await SeedLabel("bonds");

        using var response = await Client.PostAsJsonAsync(_route, Save(_bondA, -40m, [label]), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(ct);
        Assert.NotNull(dto);
        Assert.Equal(-40m, dto.ValueChange);
        Assert.Equal(_bondA, dto.BondDetailsId);
        Assert.Equal("Bond 11", dto.BondName);
        Assert.Equal("PLN", dto.Currency);
        Assert.Equal("Purchase", dto.Description);
        Assert.Equal("bonds", Assert.Single(dto.Labels).Name);
        var stored = await Db.BondEntries.SingleAsync(ct);
        Assert.Equal(dto.EntryId, stored.EntryId);
        Assert.Equal(_owner, stored.UserId);
        Assert.Null(stored.AccountId);
        Assert.Equal(0m, stored.Value);
    }

    [Fact]
    public async Task Create_RejectsInvalidInput()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var unknownBond = await Client.PostAsJsonAsync(_route, Save(_missingBond), ct);
        using var unknownLabel = await Client.PostAsJsonAsync(_route, Save(labels: [4242]), ct);
        using var ancientDate = await Client.PostAsJsonAsync(_route, Save() with { PostingDate = new DateTime(1800, 1, 1, 0, 0, 0, DateTimeKind.Utc) }, ct);
        using var longDescription = await Client.PostAsJsonAsync(_route, Save(description: new string('x', 513)), ct);

        Assert.All(new[] { unknownBond, unknownLabel, ancientDate, longDescription }, x => Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode));
        Assert.Empty(Db.BondEntries);
    }

    [Fact]
    public async Task Create_IsRefusedWhenThePlanLimitIsReached()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        _planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(false);

        using var response = await Client.PostAsJsonAsync(_route, Save(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Db.BondEntries);
    }

    [Fact]
    public async Task GuestWithNegativeId_OwnsItsEntries()
    {
        Seed();
        Authorize("guest", _guest, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var created = await Client.PostAsJsonAsync(_route, Save(), ct);
        var dto = await created.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(ct);
        using var updated = await Client.PutAsJsonAsync($"{_route}/{dto!.EntryId}", Save(valueChange: 5m), ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(_guest, (await Db.BondEntries.SingleAsync(ct)).UserId);

        using var deleted = await Client.DeleteAsync($"{_route}/{dto.EntryId}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(Db.BondEntries);
    }

    [Fact]
    public async Task AnotherOwnersOrMissingEntry_IsTheSameNotFound()
    {
        Seed();
        var foreign = await SeedEntry(_other, null, _bondA, _day, 5m);
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        var responses = new[]
        {
            await Client.PutAsJsonAsync($"{_route}/{foreign}", Save(), ct),
            await Client.PutAsJsonAsync($"{_route}/987654", Save(), ct),
            await Client.DeleteAsync($"{_route}/{foreign}", ct),
            await Client.DeleteAsync($"{_route}/987654", ct),
        };

        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NotFound, x.StatusCode));
        Assert.Equal(5m, (await Db.BondEntries.SingleAsync(ct)).ValueChange);
    }

    [Fact]
    public async Task Update_CanMoveToAnotherDefinition_AndHandlesLabels()
    {
        Seed();
        var first = await SeedLabel("first");
        var second = await SeedLabel("second");
        var id = await SeedEntry(_owner, null, _bondA, _day, 5m);
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var moved = await Client.PutAsJsonAsync($"{_route}/{id}", Save(_bondB, 30m, [first], "Moved"), ct);
        var dto = await moved.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(ct);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(_bondB, dto!.BondDetailsId);
        Assert.Equal("Bond 12", dto.BondName);
        Assert.Equal(["first"], dto.Labels.Select(x => x.Name));

        // Omitted label ids keep the labels, an explicit list replaces them, an empty list clears them.
        using var kept = await Client.PutAsJsonAsync($"{_route}/{id}", Save(_bondB, 31m), ct);
        Assert.Equal(["first"], (await kept.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(ct))!.Labels.Select(x => x.Name));
        using var swapped = await Client.PutAsJsonAsync($"{_route}/{id}", Save(_bondB, 32m, [second]), ct);
        Assert.Equal(["second"], (await swapped.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(ct))!.Labels.Select(x => x.Name));
        using var cleared = await Client.PutAsJsonAsync($"{_route}/{id}", Save(_bondB, 33m, []), ct);
        Assert.Empty((await cleared.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(ct))!.Labels);
        using var unknownBond = await Client.PutAsJsonAsync($"{_route}/{id}", Save(_missingBond), ct);
        Assert.Equal(HttpStatusCode.BadRequest, unknownBond.StatusCode);

        Db.ChangeTracker.Clear();
        var stored = await Db.BondEntries.SingleAsync(ct);
        Assert.Equal(33m, stored.ValueChange);
        Assert.Equal(_bondB, stored.BondDetailsId);
        Assert.Equal(_owner, stored.UserId);
        Assert.Null(stored.AccountId);
    }

    [Fact]
    public async Task Update_KeepsAnUnchangedDefinitionWhoseCatalogRecordIsGone()
    {
        Seed();
        var id = await SeedEntry(_owner, null, _missingBond, _day, 5m);
        Authorize("owner", _owner, UserRole.User);

        using var response = await Client.PutAsJsonAsync($"{_route}/{id}", Save(_missingBond, 6m), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(6m, (await Db.BondEntries.SingleAsync(TestContext.Current.CancellationToken)).ValueChange);
    }

    [Fact]
    public async Task HistoricalEntry_EditAndDeletePreserveDormantBalances()
    {
        Seed();
        var a1 = await SeedEntry(_owner, _accountId, _bondA, _day, 10m, value: 10m);
        var b1 = await SeedEntry(_owner, _accountId, _bondB, _day.AddDays(1), 4m, value: 4m);
        var a2 = await SeedEntry(_owner, _accountId, _bondA, _day.AddDays(2), 5m, value: 15m);
        var unlinked = await SeedEntry(_owner, null, _bondA, _day.AddDays(1), 100m);
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var moved = await Client.PutAsJsonAsync($"{_route}/{a1}", new SaveBondEntryRequest(_bondB, _day.AddDays(1).AddHours(12), 20m, "moved"), ct);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Db.ChangeTracker.Clear();
        Assert.Equal(4m, await ValueOf(b1));
        Assert.Equal(10m, await ValueOf(a1));
        Assert.Equal(15m, await ValueOf(a2));
        Assert.Equal(0m, await ValueOf(unlinked));

        using var deleted = await Client.DeleteAsync($"{_route}/{b1}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Db.ChangeTracker.Clear();
        Assert.Equal(10m, await ValueOf(a1));
        Assert.Equal(0m, await ValueOf(unlinked));
    }

    [Fact]
    public async Task Export_ListsOnlyTheOwnersRowsOfTheDefinitionInRange_LinkedOrNot_WithoutTheRunningValue()
    {
        Seed();
        await SeedEntry(_owner, _accountId, _bondA, _day.AddHours(1), 10m, value: 10m, description: "linked");
        await SeedEntry(_owner, null, _bondA, _day.AddHours(2), -3m, description: "free");
        await SeedEntry(_owner, null, _bondB, _day.AddHours(3), 99m, description: "other definition");
        await SeedEntry(_other, null, _bondA, _day.AddHours(4), 98m, description: "other owner");
        await SeedEntry(_owner, null, _bondA, _day.AddDays(40), 97m, description: "out of range");
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var response = await Client.GetAsync($"{_route}/export?bondDetailsId={_bondA}&startDate=2024-06-03T00:00:00Z&endDate=2024-06-03T23:59:59Z", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("bond-11-20240603-20240603.csv", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var lines = (await response.Content.ReadAsStringAsync(ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("Id,PostingDate,ValueChange,Bond,Description", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.EndsWith(",2024-06-03T01:00:00Z,10,Bond 11,linked", lines[1][lines[1].IndexOf(',')..]);
        Assert.EndsWith(",2024-06-03T02:00:00Z,-3,Bond 11,free", lines[2][lines[2].IndexOf(',')..]);
    }

    [Fact]
    public async Task Export_RejectsAMissingUnknownOrReversedRequest()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        var responses = new[]
        {
            await Client.GetAsync($"{_route}/export?startDate=2024-01-01&endDate=2024-12-31", ct),
            await Client.GetAsync($"{_route}/export?bondDetailsId={_missingBond}&startDate=2024-01-01&endDate=2024-12-31", ct),
            await Client.GetAsync($"{_route}/export?bondDetailsId={_bondA}&startDate=2024-12-31&endDate=2024-01-01", ct),
        };

        Assert.All(responses, x => Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode));
    }

    [Fact]
    public async Task Import_AcceptsSeveralDefinitions_ComparesAgainstEveryOwnerRow_AndOffersOnlyTheSurplus()
    {
        Seed();
        await SeedEntry(_owner, _accountId, _bondA, _day.AddHours(8), 10m, value: 10m, description: "linked statement row");
        await SeedEntry(_owner, null, _bondB, _day.AddHours(9), 7m, description: "unrelated, never offered");
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        BondEntryImportRecordDto[] rows =
        [
            new(_day.AddHours(8), 10m, _bondA, "already present"),
            new(_day.AddHours(8), 10m, _bondA, "second occurrence"),
            new(_day.AddHours(9), 8m, _bondB, "clean for bond B on a day with only unrelated rows"),
            new(_day.AddDays(1).AddHours(8), 2m, _bondB, "a clean day"),
        ];

        using var response = await Client.PostAsJsonAsync($"{_route}/import", rows, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BondImportResult>(ct);
        Assert.Equal(2, result!.Imported);
        Assert.Equal(0, result.Failed);
        var surplus = Assert.Single(result.Conflicts, c => !c.IsExactMatch);
        Assert.Equal("second occurrence", surplus.ImportEntry!.Description);
        Assert.Null(surplus.ExistingEntry);
        Assert.Single(result.Conflicts, c => c.IsExactMatch);
        Db.ChangeTracker.Clear();
        var owned = await Db.BondEntries.AsNoTracking().Where(e => e.UserId == _owner && e.AccountId == null).OrderBy(e => e.PostingDate).ToListAsync(ct);
        Assert.Equal([7m, 8m, 2m], owned.Select(e => e.ValueChange));
        Assert.All(owned, e => Assert.Equal(0m, e.Value));
        Assert.Equal(10m, (await Db.BondEntries.AsNoTracking().SingleAsync(e => e.AccountId == _accountId, ct)).Value);
    }

    [Fact]
    public async Task Import_And_Resolve_RejectAnUnknownDefinitionAndAnEmptyBody()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        BondEntryImportRecordDto[] unknown = [new(_day, 1m, _bondA), new(_day, 1m, _missingBond)];

        var responses = new[]
        {
            await Client.PostAsJsonAsync($"{_route}/import", unknown, ct),
            await Client.PostAsJsonAsync($"{_route}/import/resolve", unknown, ct),
            await Client.PostAsJsonAsync($"{_route}/import", Array.Empty<BondEntryImportRecordDto>(), ct),
            await Client.PostAsJsonAsync($"{_route}/import/resolve", Array.Empty<BondEntryImportRecordDto>(), ct),
        };

        Assert.All(responses, x => Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode));
        Assert.Empty(Db.BondEntries);
    }

    [Fact]
    public async Task Resolve_AddsOnlyTheStillMissingOccurrences_SoARetryNeverDuplicates()
    {
        Seed();
        await SeedEntry(_owner, null, _bondA, _day.AddHours(8), 10m, description: "present");
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        // The original rows of the accepted key: the occurrence that exists and two more.
        BondEntryImportRecordDto[] accepted = [new(_day.AddHours(8), 10m, _bondA, "one"), new(_day.AddHours(8), 10m, _bondA, "two"), new(_day.AddHours(8), 10m, _bondA, "three")];

        using var first = await Client.PostAsJsonAsync($"{_route}/import/resolve", accepted, ct);
        var firstResult = await first.Content.ReadFromJsonAsync<BondImportResult>(ct);
        using var retry = await Client.PostAsJsonAsync($"{_route}/import/resolve", accepted, ct);
        var retryResult = await retry.Content.ReadFromJsonAsync<BondImportResult>(ct);

        Assert.Equal(2, firstResult!.Imported);
        Assert.Equal(0, retryResult!.Imported);
        Db.ChangeTracker.Clear();
        var stored = await Db.BondEntries.AsNoTracking().OrderBy(e => e.EntryId).ToListAsync(ct);
        Assert.Equal(["present", "two", "three"], stored.Select(e => e.Description)); // the surplus keeps its own metadata
        Assert.All(stored, e => Assert.Equal(_owner, e.UserId));
    }

    [Fact]
    public async Task Resolve_RechecksThePlanLimitForWhatIsActuallyAdded()
    {
        Seed();
        await SeedEntry(_owner, null, _bondA, _day.AddHours(8), 10m);
        Authorize("owner", _owner, UserRole.User);
        // Only one more entry fits: confirming one existing occurrence plus one new one needs exactly that.
        _planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((int _, int count) => count <= 1);
        BondEntryImportRecordDto[] accepted = [new(_day.AddHours(8), 10m, _bondA), new(_day.AddHours(8), 10m, _bondA)];
        var ct = TestContext.Current.CancellationToken;

        using var fits = await Client.PostAsJsonAsync($"{_route}/import/resolve", accepted, ct);
        Assert.Equal(HttpStatusCode.OK, fits.StatusCode);

        BondEntryImportRecordDto[] tooMany = [.. accepted, new(_day.AddHours(8), 10m, _bondA), new(_day.AddHours(8), 10m, _bondA)];
        using var refused = await Client.PostAsJsonAsync($"{_route}/import/resolve", tooMany, ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(2, await Db.BondEntries.CountAsync(ct));
    }

    private async Task<decimal> ValueOf(long entryId) =>
        (await Db.BondEntries.AsNoTracking().SingleAsync(e => e.EntryId == entryId, TestContext.Current.CancellationToken)).Value;
}