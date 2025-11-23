using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.IntegrationTests;

internal sealed class TestDatabase : IDisposable
{
    public AppDbContext Context { get; }

    public TestDatabase() =>
        Context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: "Db").Options);

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
    }
}