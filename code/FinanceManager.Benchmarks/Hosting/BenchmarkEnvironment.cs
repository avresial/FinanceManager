using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Application.Identity.Seeders;
using FinanceManager.Benchmarks.Configuration;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FinanceManager.Benchmarks.Hosting;

/// <summary>
/// Process-wide singleton holding the seeded database, the running API host, an authenticated
/// <see cref="HttpClient"/> and the resolved <see cref="BenchmarkScenario"/>.
/// </summary>
/// <remarks>
/// Shared rather than per-class because BenchmarkDotNet runs <c>[GlobalSetup]</c> once per benchmark
/// class, and booting the API plus copying the database for each of them would add minutes to a run and
/// give each class a differently-aged dataset. The in-process toolchain keeps all classes in one
/// process, so a single instance serves them all.
/// </remarks>
public sealed class BenchmarkEnvironment : IDisposable
{
    private static readonly SemaphoreSlim _gate = new(1, 1);
    private static BenchmarkEnvironment? _instance;

    private readonly BenchmarkDatabase _database;
    private readonly BenchmarkApiHost _host;

    private BenchmarkEnvironment(BenchmarkDatabase database, BenchmarkApiHost host, HttpClient client, BenchmarkScenario scenario)
    {
        _database = database;
        _host = host;
        Client = client;
        Scenario = scenario;
    }

    /// <summary>API client carrying a bearer token for the seeded user.</summary>
    public HttpClient Client { get; }

    /// <summary>Ids and date window the benchmarked requests are built from.</summary>
    public BenchmarkScenario Scenario { get; }

    /// <summary>
    /// Returns the shared environment, building it on first call. Safe to await from every
    /// <c>[GlobalSetup]</c>.
    /// </summary>
    public static async Task<BenchmarkEnvironment> GetOrCreate()
    {
        if (_instance is not null) return _instance;

        await _gate.WaitAsync();
        try
        {
            return _instance ??= await Create();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Tears down the shared environment. Called once, from the process exit path.</summary>
    public static void Shutdown()
    {
        _instance?.Dispose();
        _instance = null;
    }

    public void Dispose()
    {
        Client.Dispose();
        _host.Dispose();
        // After the host, so every AppDbContext has released its SQLite handle.
        _database.Dispose();
    }

    /// <summary>
    /// Deletes every entry the write benchmarks appended to <paramref name="accountId"/> after
    /// <paramref name="postedAfter"/>, and drops the owner's cached read models.
    /// </summary>
    /// <remarks>
    /// Runs between benchmark iterations, so it goes straight to the database rather than through the
    /// delete endpoint — the point is to restore state cheaply, and routing thousands of deletes through
    /// MVC would take longer than the measurements. Invalidating the cache afterwards keeps the
    /// repository's cached ranges from outliving the rows they describe.
    /// </remarks>
    public void ResetAppendedEntries(int accountId, DateTime postedAfter)
    {
        using var scope = _host.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.CurrencyEntries
            .Where(entry => entry.AccountId == accountId && entry.PostingDate > postedAfter)
            .ExecuteDelete();

        scope.ServiceProvider.GetRequiredService<ICacheInvalidator>()
            .InvalidateUser(Scenario.UserId)
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    private static async Task<BenchmarkEnvironment> Create()
    {
        var database = await BenchmarkDatabase.Prepare(SeedTemplate);

        var host = new BenchmarkApiHost(database.ConnectionString);
        var client = host.CreateClient();
        client.BaseAddress = new Uri("http://localhost/");
        client.Timeout = TimeSpan.FromMinutes(2);
        host.AssertUsingSqlite();

        Authorize(host, client);

        var scenario = await ResolveScenario(client);

        return new BenchmarkEnvironment(database, host, client, scenario);
    }

    /// <summary>
    /// Builds the pristine dataset: the same demo history the in-app guest sandbox generates — six months
    /// of salary, rent, utilities and randomised merchant transactions across a cash and a loan account,
    /// a monthly-contribution ETF position with a full daily price series, and a bond ladder.
    /// </summary>
    /// <remarks>
    /// Reusing the product's own seeders instead of writing benchmark-specific fixtures means the shape
    /// of the data — entry counts, label distribution, account mix — tracks whatever the app actually
    /// shows a new user, and stays correct as the domain evolves.
    /// </remarks>
    private static async Task SeedTemplate(string connectionString)
    {
        using var host = new BenchmarkApiHost(connectionString);
        host.AssertUsingSqlite();
        await host.CreateSchema();

        using var scope = host.Services.CreateScope();

        var seeder = scope.ServiceProvider.GetRequiredService<GuestAccountSeeder>();
        await seeder.SeedForGuest(BenchmarkOptions.UserId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The guest seeder creates a Basic-tier user, which caps records at 10,000. The write benchmarks
        // add entries on top of the seeded history, and FM_BENCH_SEED_MONTHS can push the history well
        // past that on its own — a plan rejection would turn those iterations into 400s.
        var user = await context.Users.FirstAsync(x => x.Id == BenchmarkOptions.UserId);
        user.PricingLevel = PricingLevel.Premium;

        // Touching the currency repository once materialises the default currency rows, so no measured
        // request pays for the lazy EnsureDefaults insert.
        _ = await context.Currencies.CountAsync();

        await context.SaveChangesAsync();
    }

    private static void Authorize(BenchmarkApiHost host, HttpClient client)
    {
        using var scope = host.Services.CreateScope();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<JwtTokenGenerator>();
        var token = tokenGenerator.GenerateToken(BenchmarkOptions.UserName, BenchmarkOptions.UserId, UserRole.User);

        // Set once, outside the measured region: minting a JWT is HMAC work that has nothing to do with
        // the endpoints under test, and the API's own token lifetime is long enough to outlast a run.
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    private static async Task<BenchmarkScenario> ResolveScenario(HttpClient client)
    {
        var currencyAccountIds = await GetAccountIds(client, "api/CurrencyAccount");
        var investmentAccountIds = await GetAccountIds(client, "api/InvestmentAccount");
        var bondAccountIds = await GetAccountIds(client, "api/BondAccount");

        if (currencyAccountIds.Count == 0)
            throw new InvalidOperationException(
                "The seeded dataset contains no currency accounts. Delete the template (or set FM_BENCH_RESEED=1) and try again.");

        var (start, end) = await GetSeededWindow(client, currencyAccountIds[0]);

        return new BenchmarkScenario(
            BenchmarkOptions.UserId,
            DefaultCurrency.PLN.Id,
            start,
            end,
            currencyAccountIds,
            investmentAccountIds,
            bondAccountIds);
    }

    private static async Task<IReadOnlyList<int>> GetAccountIds(HttpClient client, string route)
    {
        var response = await client.GetAsync(route);
        response.EnsureSuccessStatusCode();

        var accounts = await response.Content.ReadFromJsonAsync<List<AvailableAccount>>();
        return accounts?.Select(x => x.AccountId).ToList() ?? [];
    }

    private static async Task<(DateTime Start, DateTime End)> GetSeededWindow(HttpClient client, int accountId)
    {
        var oldest = await client.GetFromJsonAsync<DateTime?>($"api/CurrencyEntry/Oldest/{accountId}");
        var youngest = await client.GetFromJsonAsync<DateTime?>($"api/CurrencyEntry/Youngest/{accountId}");

        if (oldest is not DateTime start || youngest is not DateTime end)
            throw new InvalidOperationException(
                $"Currency account {accountId} has no entries, so there is no date window to benchmark. Re-seed with FM_BENCH_RESEED=1.");

        return (start.Date, end.Date);
    }
}