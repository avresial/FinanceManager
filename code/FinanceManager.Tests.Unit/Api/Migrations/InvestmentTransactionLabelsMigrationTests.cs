using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FinanceManager.Tests.Unit.Api.Migrations;

public class InvestmentTransactionLabelsMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Script_CreatesOnlyJoinWithBothCascades(bool sqlServer)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (sqlServer) options.UseSqlServer("Server=localhost;Database=script-only;Trusted_Connection=True", b => b.MigrationsAssembly("FinanceManager.Api"));
        else options.UseNpgsql("Host=localhost;Database=script-only", b => b.MigrationsAssembly("FinanceManager.Api"));
        using var context = new AppDbContext(options.Options);
        var migrations = context.Database.GetMigrations().ToArray();
        var index = Array.FindIndex(migrations, x => x.EndsWith("_AddInvestmentTransactionLabels"));
        var migrator = context.GetService<IMigrator>();
        var up = migrator.GenerateScript(migrations[index - 1], migrations[index]);
        Assert.Contains("CREATE TABLE", up);
        Assert.Contains("InvestmentTransactionFinancialLabel", up);
        Assert.Contains("REFERENCES " + (sqlServer ? "[FinancialLabels]" : "\"FinancialLabels\""), up);
        Assert.Contains("REFERENCES " + (sqlServer ? "[InvestmentTransactions]" : "\"InvestmentTransactions\""), up);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(up, "ON DELETE CASCADE").Count);
        Assert.DoesNotContain("ALTER TABLE", up);
        var down = migrator.GenerateScript(migrations[index], migrations[index - 1]);
        Assert.Contains("DROP TABLE", down);
    }
}