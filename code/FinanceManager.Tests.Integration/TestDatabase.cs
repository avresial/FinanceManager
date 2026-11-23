using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Concurrent;
using Xunit;

namespace FinanceManager.Tests.Integration;

internal sealed class TestDatabase : IDisposable
{
    private static readonly ConcurrentDictionary<string, InMemoryDatabaseRoot> _databaseRoots = [];
    private static readonly ConcurrentDictionary<string, AppDbContext> _sharedContexts = [];
    private static readonly ConcurrentDictionary<string, OwnerKeyStampingInterceptor?> _interceptors = [];
    private static readonly AsyncLocal<string?> _scopedDatabaseName = new();
    private readonly string _databaseName;
    private readonly bool _shared;

    public AppDbContext Context { get; }

    /// <param name="stampOwnerKeys">Stamps the owner keys of account-linked rows seeded through the context, see <see cref="OwnerKeyStampingInterceptor"/>.</param>
    public TestDatabase(bool stampOwnerKeys = false)
    {
        _databaseName = _scopedDatabaseName.Value
            ?? Guid.NewGuid().ToString();
        _shared = _scopedDatabaseName.Value is not null;
        // A shared context is created once, so a later caller asking for different stamping would silently get the first one's.
        if (_shared && _interceptors.TryGetValue(_databaseName, out var existing) && (existing is not null) != stampOwnerKeys)
            throw new InvalidOperationException($"The shared test database '{_databaseName}' was created with stampOwnerKeys={existing is not null}.");
        Context = _shared
            ? _sharedContexts.GetOrAdd(_databaseName, name => CreateContext(name, stampOwnerKeys))
            : CreateContext(_databaseName, stampOwnerKeys);
    }

    /// <summary>Stops stamping owner keys, typically right after seeding and before real HTTP writes. No-op when stamping was not requested.</summary>
    public void StopStampingOwnerKeys()
    {
        if (_interceptors.TryGetValue(_databaseName, out var interceptor) && interceptor is not null) interceptor.Enabled = false;
    }

    internal static IDisposable UseDatabase(string databaseName) => new DatabaseScope(databaseName);

    public void Dispose()
    {
        if (_shared)
        {
            Context.ChangeTracker.Clear();
            return;
        }

        Context.Database.EnsureDeleted();
        Context.Dispose();

        // An isolated database is never reopened, so its root would otherwise sit in the static dictionary for the
        // rest of the test run — one leaked root per test. Shared roots stay: their contexts outlive this instance.
        _databaseRoots.TryRemove(_databaseName, out _);
        _interceptors.TryRemove(_databaseName, out _);
        GC.SuppressFinalize(this);
    }

    private static AppDbContext CreateContext(string databaseName, bool stampOwnerKeys)
    {
        var databaseRoot = _databaseRoots.GetOrAdd(databaseName, static _ => new InMemoryDatabaseRoot());
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName, databaseRoot);
        var interceptor = stampOwnerKeys ? new OwnerKeyStampingInterceptor() : null;
        if (interceptor is not null) options.AddInterceptors(interceptor);
        _interceptors[databaseName] = interceptor;
        return new AppDbContext(options.Options);
    }

    private sealed class DatabaseScope : IDisposable
    {
        private readonly string? _previousName = _scopedDatabaseName.Value;

        public DatabaseScope(string databaseName) => _scopedDatabaseName.Value = databaseName;

        public void Dispose() => _scopedDatabaseName.Value = _previousName;
    }
}