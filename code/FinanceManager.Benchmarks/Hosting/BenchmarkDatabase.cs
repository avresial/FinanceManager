using FinanceManager.Benchmarks.Configuration;
using Microsoft.Data.Sqlite;

namespace FinanceManager.Benchmarks.Hosting;

/// <summary>
/// Owns the SQLite file the benchmarked API reads from: a pristine seeded <em>template</em> that is
/// built once and reused, plus a per-process <em>working copy</em> that requests actually hit.
/// </summary>
/// <remarks>
/// <para>
/// The template/copy split is what makes runs comparable. Seeding produces randomised amounts and is
/// anchored to <c>UtcNow</c>, so re-seeding between a baseline and a post-refactor run would change
/// the data underneath the comparison. Building it once and copying means every run measures the same
/// bytes, and the write benchmarks can mutate freely without contaminating the next run.
/// </para>
/// <para>
/// SQLite rather than SQL Server or PostgreSQL because a benchmark that has to reach a database server
/// measures the network and that server's cache state as much as the API. A local file keeps the
/// storage cost small, constant, and identical on every machine. The trade-off is that absolute
/// numbers do not transfer to production hardware — these numbers are for comparing <em>before</em>
/// against <em>after</em>, not for capacity planning.
/// </para>
/// </remarks>
public sealed class BenchmarkDatabase : IDisposable
{
    private readonly string _workingPath;

    private BenchmarkDatabase(string workingPath) => _workingPath = workingPath;

    /// <summary>Connection string for the working copy, with pooling off so the file can be deleted on dispose.</summary>
    public string ConnectionString => BuildConnectionString(_workingPath);

    /// <summary>Absolute path of the working copy.</summary>
    public string Path => _workingPath;

    /// <summary>True when this run had to build the template from scratch.</summary>
    public bool TemplateWasCreated { get; private init; }

    /// <summary>
    /// Ensures a seeded template exists (building it via <paramref name="seedTemplate"/> if needed) and
    /// hands back a fresh working copy of it.
    /// </summary>
    /// <param name="seedTemplate">
    /// Populates a brand-new, empty database at the given path. Only invoked when no reusable template
    /// is present.
    /// </param>
    public static async Task<BenchmarkDatabase> Prepare(Func<string, Task> seedTemplate)
    {
        Directory.CreateDirectory(BenchmarkOptions.DataDirectory);

        var templatePath = BenchmarkOptions.TemplateDatabasePath;
        var created = false;

        if (BenchmarkOptions.ForceReseed)
            DeleteDatabaseFiles(templatePath);

        if (!File.Exists(templatePath))
        {
            // Seed into a temporary file and move it into place only on success, so an interrupted or
            // failed seed can never leave a half-populated template that later runs would silently reuse.
            var stagingPath = templatePath + ".staging";
            DeleteDatabaseFiles(stagingPath);

            await seedTemplate(BuildConnectionString(stagingPath));

            SqliteConnection.ClearAllPools();
            File.Move(stagingPath, templatePath, overwrite: true);
            created = true;
        }

        var workingPath = System.IO.Path.Combine(
            BenchmarkOptions.DataDirectory,
            $"run-{Environment.ProcessId}.sqlite");

        DeleteDatabaseFiles(workingPath);
        File.Copy(templatePath, workingPath);

        return new BenchmarkDatabase(workingPath) { TemplateWasCreated = created };
    }

    public void Dispose()
    {
        // Every AppDbContext in the host is disposed by now, but SQLite keeps pooled handles per
        // connection string; clearing them releases the file so the delete succeeds on Windows too.
        SqliteConnection.ClearAllPools();
        DeleteDatabaseFiles(_workingPath);
    }

    private static string BuildConnectionString(string path) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = path,
            // Pooling keeps the file locked after the last context is disposed, which breaks the
            // working-copy cleanup. Connection setup against a local file is cheap enough that the
            // measured endpoints do not notice.
            Pooling = false,
        }.ToString();

    private static void DeleteDatabaseFiles(string path)
    {
        // WAL and shared-memory sidecars must go too, or a stale -wal replays into the fresh copy.
        foreach (var candidate in new[] { path, path + "-wal", path + "-shm" })
        {
            if (File.Exists(candidate))
                File.Delete(candidate);
        }
    }
}