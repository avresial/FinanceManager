using FinanceManager.Domain.Alerts.Entities;
using FinanceManager.Domain.Alerts.Enums;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Infrastructure.Features.Alerts.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

[Trait("Category", "Integration")]
public sealed class SqliteFinancialAlertRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly CommandCounter _commands = new();
    private readonly AppDbContext _context;

    public SqliteFinancialAlertRepositoryTests()
    {
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_commands)
            .Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetAllTimeEvaluationData_UsesOneCommandForMultipleAlerts()
    {
        var dining = new FinancialLabel { Id = 5, Name = "Dining" };
        _context.FinancialLabels.Add(dining);
        _context.Accounts.Add(new FinancialAccountBaseDto
        {
            AccountId = 1,
            UserId = 1,
            Name = "Cash",
            AccountType = AccountType.Currency,
            AccountLabel = AccountLabel.Cash
        });
        _context.CurrencyEntries.AddRange(
            new CurrencyAccountEntry(1, 1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 400m, -400m)
            {
                Description = "Cafe",
                Labels = [dining]
            },
            new CurrencyAccountEntry(1, 2, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), 3300m, -2900m)
            {
                Description = "Acme Store",
                ContractorDetails = "Acme",
                Labels = [dining]
            });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _commands.Reset();

        var alerts = new[]
        {
            new FinancialAlert(
                1,
                "Dining total",
                AlertType.CategorySpending,
                AlertComparisonOperator.GreaterThan,
                1000m,
                evaluationPeriod: AlertEvaluationPeriod.AllTime,
                labelName: "dining"),
            new FinancialAlert(
                1,
                "Acme total",
                AlertType.MerchantSpending,
                AlertComparisonOperator.GreaterThan,
                1000m,
                evaluationPeriod: AlertEvaluationPeriod.AllTime,
                merchantName: "acme"),
            new FinancialAlert(
                1,
                "Large purchase",
                AlertType.LargeTransaction,
                AlertComparisonOperator.GreaterThan,
                2000m,
                evaluationPeriod: AlertEvaluationPeriod.AllTime)
            {
                CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new FinancialAlert(
                1,
                "No large purchase",
                AlertType.LargeTransaction,
                AlertComparisonOperator.GreaterThan,
                5000m,
                evaluationPeriod: AlertEvaluationPeriod.AllTime)
            {
                CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var result = await new FinancialAlertRepository(_context).GetAllTimeEvaluationData(
            1,
            alerts,
            new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc),
            TestContext.Current.CancellationToken);

        Assert.Equal(4, result.Count);
        Assert.Equal(3300m, result[alerts[0].Id].TotalSpend);
        Assert.Equal(2900m, result[alerts[1].Id].TotalSpend);
        Assert.Equal(2, result[alerts[2].Id].LargestTransaction!.EntryId);
        Assert.Null(result[alerts[3].Id].LargestTransaction);
        Assert.Equal(1, _commands.Count);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Reset() => Interlocked.Exchange(ref _count, 0);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }
    }
}