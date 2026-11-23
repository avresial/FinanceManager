using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class RetireTransactionRuleAccountConditionsMigrationTests
{
    private const string _previous = "AddOwnerTransactionHistoryIndexes";
    private const string _migration = "RetireTransactionRuleAccountConditions";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpScript_RemovesAccountConditionsInOrder_AndDisablesOnlyAffectedRules(bool sqlServer)
    {
        var up = Script(sqlServer, _previous, _migration).Replace("\"", "").Replace("[", "").Replace("]", "");

        Assert.Contains("UPDATE", up);
        Assert.Contains("TransactionRules", up);
        // Case-insensitive, trimmed match on the condition type, both key casings.
        Assert.Contains(sqlServer ? "LOWER(LTRIM(RTRIM(COALESCE(" : "LOWER(BTRIM(COALESCE(", up);
        Assert.Contains("'account'", up, StringComparison.OrdinalIgnoreCase);
        // Remaining conditions keep their order; nothing left becomes an empty array.
        Assert.Contains(sqlServer ? "WITHIN GROUP (ORDER BY CAST(e.key AS int))" : "ORDER BY e.ord", up);
        Assert.Contains("COALESCE(", up);
        Assert.Contains(sqlServer ? "r.IsEnabled = 0" : "IsEnabled = FALSE", up);
        Assert.Contains("UpdatedAt", up);
        // Only rows that still hold an account condition are rewritten, which also makes a second run a no-op.
        Assert.Contains("EXISTS", up);
        // The step touches data only.
        Assert.DoesNotContain("ALTER TABLE", up);
        Assert.DoesNotContain("CREATE ", up);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DownScript_IsADocumentedNoOpBeyondTheHistoryRow(bool sqlServer)
    {
        var down = Script(sqlServer, _migration, _previous).Replace("\"", "").Replace("[", "").Replace("]", "");

        Assert.DoesNotContain("TransactionRules", down);
        Assert.DoesNotContain("ALTER TABLE", down);
    }

    private static string Script(bool sqlServer, string from, string to)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer)
            options.UseSqlServer("Server=localhost;Database=script-only;Trusted_Connection=True", b => b.MigrationsAssembly("FinanceManager.Api"));
        else
            options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        return context.GetService<IMigrator>().GenerateScript(from, to);
    }
}