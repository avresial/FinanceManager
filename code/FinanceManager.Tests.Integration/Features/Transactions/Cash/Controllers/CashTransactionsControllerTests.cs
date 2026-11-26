using FinanceManager.Api.Features.Labels.Services;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Tests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace FinanceManager.Tests.Integration.Features.Transactions.Cash.Controllers;

[Trait("Category", "Integration")]
public class CashTransactionsControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const string _route = "api/transactions/cash";
    private const int _owner = 1;
    private const int _other = 2;
    private const int _guest = -7;
    private const int _pln = 0;
    private const int _usd = 1;
    private static readonly DateTime _day = new(2024, 6, 3, 0, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IUserPlanVerifier> _planVerifier = new();
    private readonly Mock<ILabelSetterChannel> _labelSetter = new();
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
        services.AddSingleton(_labelSetter.Object);
    }

    public override void Dispose()
    {
        base.Dispose();
        _database?.Dispose();
        _database = null;
        GC.SuppressFinalize(this);
    }

    private AppDbContext Db => _database!.Context;

    private void Seed()
    {
        Db.Currencies.Add(new Currency(_usd, "USD", "$"));
        Db.SaveChanges();
    }

    private async Task<long> SeedEntry(int userId, int currencyId, DateTime date, decimal valueChange, string description = "seed")
    {
        var entry = new CashEntry(0, date, valueChange)
        {
            UserId = userId,
            CurrencyId = currencyId,
            Description = description,
        };
        Db.CurrencyEntries.Add(entry);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return entry.EntryId;
    }

    private static CreateCashEntryRequest Create(int currency = _pln, decimal valueChange = -12.5m, IReadOnlyList<int>? labels = null, DateTime? date = null) =>
        new(currency, date ?? _day.AddHours(9), valueChange, "Coffee", "Cafe", labels);

    private static UpdateCashEntryRequest Update(decimal valueChange = 30m, IReadOnlyList<int>? labels = null, DateTime? date = null) =>
        new(date ?? _day.AddHours(10), valueChange, "Salary", "Employer", labels);

    private async Task<int> SeedLabel(string name)
    {
        var label = new FinancialLabel { Name = name };
        Db.FinancialLabels.Add(label);
        await Db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return label.Id;
    }

    [Fact]
    public async Task Anonymous_IsUnauthorizedOnEveryRoute()
    {
        ClearAuthorization();
        var ct = TestContext.Current.CancellationToken;
        var responses = new[]
        {
            await Client.PostAsJsonAsync(_route, Create(), ct),
            await Client.PutAsJsonAsync(_route + "/1", Update(), ct),
            await Client.DeleteAsync(_route + "/1", ct),
            await Client.PostAsJsonAsync(_route + "/import?currencyId=0", new[] { new CurrencyEntryImportRecordDto(_day, 1m) }, ct),
            await Client.GetAsync(_route + "/export?currencyId=0&startDate=2024-01-01&endDate=2024-12-31", ct),
            await Client.GetAsync(_route + "?entryIds=1", ct),
        };
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.Unauthorized, x.StatusCode));
    }

    [Fact]
    public async Task Create_StoresAnOwnerRowWithASignedDelta_AndReturnsThePersistedRow()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        var label = await SeedLabel("food");

        using var response = await Client.PostAsJsonAsync(_route, Create(_pln, -12.5m, [label]), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct);
        Assert.NotNull(dto);
        Assert.Equal(-12.5m, dto.ValueChange);
        Assert.Equal(_pln, dto.CurrencyId);
        Assert.Equal("PLN", dto.Currency);
        Assert.Equal("Coffee", dto.Description);
        Assert.Equal("Cafe", dto.ContractorDetails);
        Assert.Equal("food", Assert.Single(dto.Labels).Name);
        var stored = await Db.CurrencyEntries.SingleAsync(ct);
        Assert.Equal(dto.EntryId, stored.EntryId);
        Assert.Equal(_owner, stored.UserId);
        Assert.Equal(_pln, stored.CurrencyId);
        Assert.Equal(_day.AddHours(9), stored.PostingDate);
    }

    [Fact]
    public async Task Create_QueuesAnUnlabelledDescribedEntryForAiLabelingUnderItsOwner()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var response = await Client.PostAsJsonAsync(_route, Create(), ct);

        var dto = await response.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct);
        _labelSetter.Verify(x => x.QueueEntries(_owner,
            It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new[] { dto!.EntryId })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_DoesNotQueuePreLabelledEntriesOrGuestSandboxes()
    {
        Seed();
        var ct = TestContext.Current.CancellationToken;
        var label = await SeedLabel("food");
        Authorize("owner", _owner, UserRole.User);
        using var labelled = await Client.PostAsJsonAsync(_route, Create(labels: [label]), ct);
        Authorize("guest", _guest, UserRole.User);
        using var guest = await Client.PostAsJsonAsync(_route, Create(), ct);

        Assert.Equal(HttpStatusCode.OK, labelled.StatusCode);
        Assert.Equal(HttpStatusCode.OK, guest.StatusCode);
        _labelSetter.Verify(x => x.QueueEntries(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIds_ReturnsTheOwnersEntriesNewestFirst_AndSkipsForeignAndMissingIds()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        if (!Db.Currencies.Any(currency => currency.Id == _pln)) Db.Currencies.Add(new Currency(_pln, "PLN", "zł"));
        var older = await SeedEntry(_owner, _pln, _day, -5m, description: "older");
        var newer = await SeedEntry(_owner, _pln, _day.AddDays(2), -7m, description: "newer");
        var foreign = await SeedEntry(_other, _pln, _day.AddDays(1), -9m);

        var response = await Client.GetFromJsonAsync<List<CurrencyTransactionLogItemDto>>(
            $"{_route}?entryIds={older}&entryIds={newer}&entryIds={foreign}&entryIds=9999&entryIds={newer}", ct);

        Assert.Equal([newer, older], response!.Select(entry => entry.EntryId));
        Assert.All(response!, entry => Assert.Equal("PLN", entry.Currency));
    }

    [Fact]
    public async Task GetByIds_RejectsAnEmptyOversizedOrNonPositiveIdList()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var none = await Client.GetAsync(_route, ct);
        using var tooMany = await Client.GetAsync($"{_route}?{string.Join('&', Enumerable.Range(1, 101).Select(id => $"entryIds={id}"))}", ct);
        using var maximum = await Client.GetAsync($"{_route}?{string.Join('&', Enumerable.Range(1, 100).Select(id => $"entryIds={id}"))}", ct);
        using var nonPositive = await Client.GetAsync($"{_route}?entryIds=0", ct);

        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.OK, maximum.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, nonPositive.StatusCode);
    }

    [Fact]
    public async Task GetByIds_ForAGuest_ReadsOnlyTheGuestsOwnEntries()
    {
        Seed();
        Authorize("guest", _guest, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        var own = await SeedEntry(_guest, _pln, _day, -5m);
        var owners = await SeedEntry(_owner, _pln, _day, -5m);

        var response = await Client.GetFromJsonAsync<List<CurrencyTransactionLogItemDto>>($"{_route}?entryIds={own}&entryIds={owners}", ct);

        Assert.Equal([own], response!.Select(entry => entry.EntryId));
    }

    [Fact]
    public async Task Create_InAnotherCurrency_KeepsTheCurrencyPartitionsApart()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var response = await Client.PostAsJsonAsync(_route, Create(_usd), ct);

        var dto = await response.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct);
        Assert.Equal("USD", dto!.Currency);
        Assert.Equal(_usd, (await Db.CurrencyEntries.SingleAsync(ct)).CurrencyId);
    }

    [Fact]
    public async Task Create_RejectsInvalidInput()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var omittedCurrency = await Client.PostAsync(_route,
            new StringContent("""{"postingDate":"2024-06-03T09:00:00Z","valueChange":1,"description":"x"}""", Encoding.UTF8, "application/json"), ct);
        using var unknownCurrency = await Client.PostAsJsonAsync(_route, Create(currency: 999), ct);
        using var unknownLabel = await Client.PostAsJsonAsync(_route, Create(labels: [12345]), ct);
        using var ancientDate = await Client.PostAsJsonAsync(_route, Create(date: new DateTime(1800, 1, 1, 0, 0, 0, DateTimeKind.Utc)), ct);
        using var missingDescription = await Client.PostAsJsonAsync(_route, Create() with { Description = "" }, ct);
        using var longContractor = await Client.PostAsJsonAsync(_route, Create() with { ContractorDetails = new string('x', 513) }, ct);

        Assert.All(new[] { omittedCurrency, unknownCurrency, unknownLabel, ancientDate, missingDescription, longContractor },
            x => Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode));
        Assert.Empty(Db.CurrencyEntries);
    }

    [Fact]
    public async Task Create_IsRefusedWhenThePlanLimitIsReached()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        _planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(false);

        using var response = await Client.PostAsJsonAsync(_route, Create(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Db.CurrencyEntries);
    }

    [Fact]
    public async Task GuestWithNegativeId_OwnsItsEntries()
    {
        Seed();
        Authorize("guest", _guest, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var created = await Client.PostAsJsonAsync(_route, Create(), ct);
        var dto = await created.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct);
        using var updated = await Client.PutAsJsonAsync($"{_route}/{dto!.EntryId}", Update(), ct);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(_guest, (await Db.CurrencyEntries.SingleAsync(ct)).UserId);

        using var deleted = await Client.DeleteAsync($"{_route}/{dto.EntryId}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(Db.CurrencyEntries);
    }

    [Fact]
    public async Task AnotherOwnersOrMissingEntry_IsTheSameNotFound()
    {
        Seed();
        var foreign = await SeedEntry(_other, _pln, _day, 5m);
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        var responses = new[]
        {
            await Client.PutAsJsonAsync($"{_route}/{foreign}", Update(), ct),
            await Client.PutAsJsonAsync($"{_route}/987654", Update(), ct),
            await Client.DeleteAsync($"{_route}/{foreign}", ct),
            await Client.DeleteAsync($"{_route}/987654", ct),
        };

        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NotFound, x.StatusCode));
        var untouched = await Db.CurrencyEntries.SingleAsync(ct);
        Assert.Equal(5m, untouched.ValueChange);
        Assert.Equal(_other, untouched.UserId);
    }

    [Fact]
    public async Task Update_KeepsTheCurrencyAndOwner_AndHandlesLabels()
    {
        Seed();
        var first = await SeedLabel("first");
        var second = await SeedLabel("second");
        var id = await SeedEntry(_owner, _usd, _day, 5m);
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var replaced = await Client.PutAsJsonAsync($"{_route}/{id}", Update(30m, [first]), ct);
        var dto = await replaced.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal(30m, dto!.ValueChange);
        Assert.Equal("USD", dto.Currency);
        Assert.Equal("Salary", dto.Description);
        Assert.Equal(["first"], dto.Labels.Select(x => x.Name));

        // Omitted label ids keep the labels, an explicit list replaces them, an empty list clears them.
        using var kept = await Client.PutAsJsonAsync($"{_route}/{id}", Update(31m), ct);
        Assert.Equal(["first"], (await kept.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct))!.Labels.Select(x => x.Name));
        using var swapped = await Client.PutAsJsonAsync($"{_route}/{id}", Update(32m, [second]), ct);
        Assert.Equal(["second"], (await swapped.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct))!.Labels.Select(x => x.Name));
        using var cleared = await Client.PutAsJsonAsync($"{_route}/{id}", Update(33m, []), ct);
        Assert.Empty((await cleared.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(ct))!.Labels);
        using var unknown = await Client.PutAsJsonAsync($"{_route}/{id}", Update(labels: [4242]), ct);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        Db.ChangeTracker.Clear();
        var stored = await Db.CurrencyEntries.SingleAsync(ct);
        Assert.Equal(33m, stored.ValueChange);
        Assert.Equal(_usd, stored.CurrencyId);
        Assert.Equal(_owner, stored.UserId);
    }

    [Fact]
    public async Task Export_ListsOnlyTheOwnersRowsOfTheCurrencyInRange()
    {
        Seed();
        await SeedEntry(_owner, _pln, _day.AddHours(1), 10m, description: "first");
        await SeedEntry(_owner, _pln, _day.AddHours(2), -3m, description: "free, \"quoted\"");
        await SeedEntry(_owner, _usd, _day.AddHours(3), 99m, description: "other currency");
        await SeedEntry(_other, _pln, _day.AddHours(4), 98m, description: "other owner");
        await SeedEntry(_owner, _pln, _day.AddDays(40), 97m, description: "out of range");
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;

        using var response = await Client.GetAsync($"{_route}/export?currencyId={_pln}&startDate=2024-06-03T00:00:00Z&endDate=2024-06-03T23:59:59Z", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("cash-pln-20240603-20240603.csv", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var lines = (await response.Content.ReadAsStringAsync(ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("Id,PostingDate,ValueChange,ContractorDetails,Description", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.EndsWith(",2024-06-03T01:00:00Z,10,,first", lines[1][lines[1].IndexOf(',')..]);
        Assert.Contains("-3", lines[2]);
        Assert.Contains("\"free, \"\"quoted\"\"\"", lines[2]);
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
            await Client.GetAsync($"{_route}/export?currencyId=999&startDate=2024-01-01&endDate=2024-12-31", ct),
            await Client.GetAsync($"{_route}/export?currencyId=0&startDate=2024-12-31&endDate=2024-01-01", ct),
        };

        Assert.All(responses, x => Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode));
    }

    [Fact]
    public async Task Import_RunsAsAnOwnerJob_SkipsExactMatches_AndSavesTheReviewedSurplusOnce()
    {
        Seed();
        await SeedEntry(_owner, _pln, _day.AddHours(8), 10m, description: "statement row");
        await SeedEntry(_owner, _pln, _day.AddHours(20), 7m, description: "unrelated, never offered");
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        CurrencyEntryImportRecordDto[] rows =
        [
            new(_day.AddHours(8), 10m, "Shop", "already present"),
            new(_day.AddHours(8), 5m, "Shop", "surplus on the same day"),
            new(_day.AddDays(1).AddHours(8), 2m, null, "a clean day"),
        ];

        using var start = await Client.PostAsJsonAsync($"{_route}/import?currencyId={_pln}", rows, ct);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var jobId = (await start.Content.ReadFromJsonAsync<CurrencyImportJobStartResponseDto>(ct))!.JobId;
        var status = await WaitForJob(jobId, ct);

        Assert.Equal(1, status.Imported); // the clean day
        var actionable = status.Conflicts.Where(c => !c.Conflict.IsExactMatch).ToList();
        var conflict = Assert.Single(actionable);
        Assert.Equal("surplus on the same day", conflict.Conflict.ImportEntry!.Description);
        Assert.Null(conflict.Conflict.ExistingEntry);

        // Another owner never sees the job; an unknown conflict id is rejected.
        Authorize("other", _other, UserRole.User);
        using var foreign = await Client.GetAsync($"api/transactions/cash/import/jobs/{jobId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Authorize("owner", _owner, UserRole.User);
        using var bogus = await Client.PostAsJsonAsync($"api/transactions/cash/import/jobs/{jobId}/resolve",
            new CurrencyImportResolveConflictsRequestDto { JobId = jobId, Decisions = [new("not-a-conflict", true, false)] }, ct);
        Assert.Equal(HttpStatusCode.NotFound, bogus.StatusCode);

        // Accepting the surplus adds it once; repeating the decision is a no-op.
        var request = new CurrencyImportResolveConflictsRequestDto { JobId = jobId, Decisions = [new(conflict.ConflictId, PickImported: true, PickExisting: false)] };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var resolved = await Client.PostAsJsonAsync($"api/transactions/cash/import/jobs/{jobId}/resolve", request, ct);
            Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        }

        Db.ChangeTracker.Clear();
        var owned = await Db.CurrencyEntries.AsNoTracking().Where(e => e.UserId == _owner).OrderBy(e => e.PostingDate).ThenBy(e => e.EntryId).ToListAsync(ct);
        Assert.Equal([10m, 5m, 7m, 2m], owned.Select(e => e.ValueChange)); // the statement row, surplus once, the unrelated row, the clean day
        Assert.All(owned, e => Assert.Equal(_pln, e.CurrencyId));
    }

    [Fact]
    public async Task ImportDecision_ThatCannotBeSavedIsReopened_SoItCanBeRetried()
    {
        Seed();
        await SeedEntry(_owner, _pln, _day.AddHours(8), 10m);
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        CurrencyEntryImportRecordDto[] rows = [new(_day.AddHours(8), 10m, null, "present"), new(_day.AddHours(8), 10m, null, "surplus")];
        using var start = await Client.PostAsJsonAsync($"{_route}/import?currencyId={_pln}", rows, ct);
        var jobId = (await start.Content.ReadFromJsonAsync<CurrencyImportJobStartResponseDto>(ct))!.JobId;
        var conflict = Assert.Single((await WaitForJob(jobId, ct)).Conflicts, c => !c.Conflict.IsExactMatch);
        var request = new CurrencyImportResolveConflictsRequestDto { JobId = jobId, Decisions = [new(conflict.ConflictId, PickImported: true, PickExisting: false)] };

        // The plan limit is rechecked for the approved addition: refused, nothing saved, the decision stays open.
        _planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(false);
        using var refused = await Client.PostAsJsonAsync($"api/transactions/cash/import/jobs/{jobId}/resolve", request, ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(1, await Db.CurrencyEntries.CountAsync(ct));
        var open = await Client.GetFromJsonAsync<CurrencyImportJobStatusDto>($"api/transactions/cash/import/jobs/{jobId}", ct);
        Assert.Equal(0, open!.ResolvedConflictCount);

        _planVerifier.Setup(x => x.CanAddMoreEntries(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        using var retried = await Client.PostAsJsonAsync($"api/transactions/cash/import/jobs/{jobId}/resolve", request, ct);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Db.ChangeTracker.Clear();
        Assert.Equal(2, await Db.CurrencyEntries.CountAsync(ct));
        Assert.Equal("surplus", (await Db.CurrencyEntries.AsNoTracking().OrderByDescending(e => e.EntryId).FirstAsync(ct)).Description);
    }

    [Fact]
    public async Task ImportDecision_OfAnotherOwnerOrAnotherJobsConflictId_IsNotFoundAndSavesNothing()
    {
        Seed();
        await SeedEntry(_owner, _pln, _day.AddHours(8), 10m);
        await SeedEntry(_owner, _usd, _day.AddHours(8), 10m);
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        var plnConflict = await StartJobWithASurplus(_pln, ct);
        var usdConflict = await StartJobWithASurplus(_usd, ct);
        var rowsBefore = await Db.CurrencyEntries.CountAsync(ct);

        // The conflict id of the caller's own job in another currency does not resolve here.
        using var crossJob = await Client.PostAsJsonAsync($"api/transactions/cash/import/jobs/{plnConflict.JobId}/resolve",
            new CurrencyImportResolveConflictsRequestDto { JobId = plnConflict.JobId, Decisions = [new(usdConflict.ConflictId, true, false)] }, ct);
        Assert.Equal(HttpStatusCode.NotFound, crossJob.StatusCode);

        // Another owner cannot resolve the job even with a valid conflict id.
        Authorize("other", _other, UserRole.User);
        using var foreign = await Client.PostAsJsonAsync($"api/transactions/cash/import/jobs/{plnConflict.JobId}/resolve",
            new CurrencyImportResolveConflictsRequestDto { JobId = plnConflict.JobId, Decisions = [new(plnConflict.ConflictId, true, false)] }, ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        Db.ChangeTracker.Clear();
        Assert.Equal(rowsBefore, await Db.CurrencyEntries.CountAsync(ct));
    }

    private async Task<(Guid JobId, string ConflictId)> StartJobWithASurplus(int currencyId, CancellationToken ct)
    {
        CurrencyEntryImportRecordDto[] rows = [new(_day.AddHours(8), 10m, null, "present"), new(_day.AddHours(8), 10m, null, "surplus")];
        using var start = await Client.PostAsJsonAsync($"{_route}/import?currencyId={currencyId}", rows, ct);
        var jobId = (await start.Content.ReadFromJsonAsync<CurrencyImportJobStartResponseDto>(ct))!.JobId;
        var conflict = Assert.Single((await WaitForJob(jobId, ct)).Conflicts, c => !c.Conflict.IsExactMatch);
        return (jobId, conflict.ConflictId);
    }

    [Fact]
    public async Task Import_RejectsAnUnknownCurrencyAndAnEmptyBody()
    {
        Seed();
        Authorize("owner", _owner, UserRole.User);
        var ct = TestContext.Current.CancellationToken;
        CurrencyEntryImportRecordDto[] rows = [new(_day, 1m)];

        using var unknown = await Client.PostAsJsonAsync($"{_route}/import?currencyId=999", rows, ct);
        using var omitted = await Client.PostAsJsonAsync($"{_route}/import", rows, ct);
        using var empty = await Client.PostAsJsonAsync($"{_route}/import?currencyId=0", Array.Empty<CurrencyEntryImportRecordDto>(), ct);

        Assert.All(new[] { unknown, omitted, empty }, x => Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode));
    }

    private async Task<CurrencyImportJobStatusDto> WaitForJob(Guid jobId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var status = await Client.GetFromJsonAsync<CurrencyImportJobStatusDto>($"api/transactions/cash/import/jobs/{jobId}", ct);
            if (status is { IsCompleted: true }) return status;
            await Task.Delay(50, ct);
        }

        throw new TimeoutException("The import job did not finish.");
    }
}