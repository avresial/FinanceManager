using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class MigrateAlertsToCurrencyScopeMigrationTests
{
    private const string _previous = "RetireTransactionRuleAccountConditions";
    private const string _migration = "MigrateAlertsToCurrencyScope";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpScript_AddsANullableCurrencyColumnWithoutDroppingAnything(bool sqlServer)
    {
        var up = Normalize(Script(sqlServer, _previous, _migration));

        // Nullable: PostgreSQL columns are nullable by default, SQL Server states NULL explicitly.
        Assert.Contains(sqlServer ? "ALTER TABLE FinancialAlerts ADD CurrencyId integer NULL;" : "ALTER TABLE FinancialAlerts ADD CurrencyId integer;", up);
        Assert.DoesNotContain("CurrencyId integer NOT NULL", up);
        Assert.DoesNotContain("DROP COLUMN", up);
        Assert.DoesNotContain("DROP TABLE", up);
        Assert.DoesNotContain("DEFAULT", up);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpScript_PrefillsOnlyExactCurrencyBeforeClearingTheAccountScope(bool sqlServer)
    {
        var up = Normalize(Script(sqlServer, _previous, _migration));

        var prefill = up.IndexOf("SELECT a.CurrencyId", StringComparison.Ordinal);
        var clear = up.IndexOf("SET AccountId = NULL", StringComparison.Ordinal);
        Assert.True(prefill >= 0, "currency is read from the alert's account");
        Assert.True(clear > prefill, "AccountId is cleared only after the currency was copied from it");
        // Exact only: the account must belong to the alert's user and be a Currency account (type 0).
        Assert.Contains("a.UserId = fa.UserId", up);
        Assert.Contains("a.AccountType = 0", up);
        // Alerts without an account are never given a guessed currency.
        Assert.Contains("fa.AccountId IS NOT NULL", up);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpScript_DisablesTheFourCashTypesAndResetsTheirTriggerStateOnly(bool sqlServer)
    {
        var up = Normalize(Script(sqlServer, _previous, _migration));

        Assert.Contains("AlertType IN (1, 2, 3, 4)", up);
        Assert.DoesNotContain("AlertType IN (1, 2, 3, 4, 5)", up);
        Assert.DoesNotContain("AlertType = 5", up);
        Assert.Contains(sqlServer ? "IsEnabled = 0" : "IsEnabled = FALSE", up);
        Assert.Contains("LastStatus = 1", up);
        Assert.Contains("LastTriggeredConditionFingerprint = NULL", up);
        // No fingerprint translation or seeding: the evaluators produce fresh fingerprints once the alert is re-enabled.
        Assert.DoesNotContain("SUBSTRING", up);
        Assert.DoesNotContain("STRING_AGG", up);
        Assert.DoesNotContain("REGEXP_REPLACE", up);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UpScript_IsRepeatableAndTouchesOnlyRowsThatStillNeedIt(bool sqlServer)
    {
        var up = Normalize(Script(sqlServer, _previous, _migration));

        Assert.Contains("fa.CurrencyId IS NULL", up);
        Assert.Contains(sqlServer ? "IsEnabled = 1 OR LastStatus <> 1" : "IsEnabled = TRUE OR LastStatus <> 1", up);
        Assert.Contains("WHERE AccountId IS NOT NULL", up);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DownScript_DropsOnlyTheCurrencyColumn(bool sqlServer)
    {
        var down = Normalize(Script(sqlServer, _migration, _previous));

        Assert.Contains("DROP COLUMN CurrencyId", down);
        Assert.DoesNotContain("AccountId", down);
    }

    private static string Normalize(string script) => script.Replace("\"", "").Replace("[", "").Replace("]", "");

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