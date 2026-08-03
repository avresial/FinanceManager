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
    private static readonly AsyncLocal<string?> _scopedDatabaseName = new();
    private readonly string _databaseName;
    private readonly bool _shared;

    public AppDbContext Context { get; }

    public TestDatabase()
    {
        _databaseName = _scopedDatabaseName.Value
            ?? Guid.NewGuid().ToString();
        _shared = _scopedDatabaseName.Value is not null;
        Context = _shared
            ? _sharedContexts.GetOrAdd(_databaseName, static name => CreateContext(name))
            : CreateContext(_databaseName);
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
        GC.SuppressFinalize(this);
    }

    private static AppDbContext CreateContext(string databaseName)
    {
        var databaseRoot = _databaseRoots.GetOrAdd(databaseName, static _ => new InMemoryDatabaseRoot());
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .Options);
    }

    private sealed class DatabaseScope : IDisposable
    {
        private readonly string? _previousName = _scopedDatabaseName.Value;

        public DatabaseScope(string databaseName) => _scopedDatabaseName.Value = databaseName;

        public void Dispose() => _scopedDatabaseName.Value = _previousName;
    }
}