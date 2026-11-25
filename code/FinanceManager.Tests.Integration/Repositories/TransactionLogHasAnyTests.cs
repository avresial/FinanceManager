using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Investments.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Bonds.Repositories;
using FinanceManager.Infrastructure.Features.Transactions.Cash.Repositories;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

/// <summary>The Home / navigation existence probe over the real owner readers: each kind alone, other owners, guest ids.</summary>
[Trait("Category", "Integration")]
public sealed class TransactionLogHasAnyTests : IDisposable
{
    private const int _owner = 1;
    private const int _other = 2;
    private const int _guest = -3;
    private static readonly DateTime _day = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly TransactionLogService _service;

    public TransactionLogHasAnyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _context.AssetListings.Add(new AssetListing
        {
            Id = 1,
            Ticker = "ABC",
            Asset = new Asset { Id = 1, Name = "Abc" },
            ExchangeMic = "XWAR",
            ExchangeName = "Warsaw",
            TradingCurrency = "PLN"
        });
        _context.SaveChanges();
        _service = new TransactionLogService(new CashTransactionRepository(_context), new BondTransactionRepository(_context),
            new InvestmentTransactionRepository(_context), new DividendPaymentRepository(_context));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<bool> HasAny(int userId) => await _service.HasAny(userId, Token);

    private Task Add(string kind, int owner)
    {
        switch (kind)
        {
            case "cash":
                _context.CurrencyEntries.Add(new CurrencyAccountEntry(null, 0, _day, 0, 5) { UserId = owner, CurrencyId = 0 });
                break;
            case "bond":
                _context.BondEntries.Add(new BondAccountEntry(null, 0, _day, 0, 1, 5) { UserId = owner });
                break;
            case "trade":
                _context.InvestmentTransactions.Add(new InvestmentTransaction
                {
                    UserId = owner,
                    AssetListingId = 1,
                    TradeDate = DateOnly.FromDateTime(_day),
                    Type = InvestmentTransactionType.Buy,
                    Quantity = 1,
                    UnitPrice = 1,
                    Currency = "PLN"
                });
                break;
            case "dividend":
                _context.DividendPayments.Add(new DividendPayment
                {
                    UserId = owner,
                    AssetListingId = 1,
                    PaymentDate = DateOnly.FromDateTime(_day),
                    Amount = 1,
                    Currency = "PLN"
                });
                break;
        }
        return _context.SaveChangesAsync(Token);
    }

    [Fact]
    public async Task OwnerWithNothing_HasNone()
    {
        Assert.False(await HasAny(_owner));
        Assert.False(await HasAny(_guest));
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("bond")]
    [InlineData("trade")]
    [InlineData("dividend")]
    public async Task EachSingleKind_IsEnough_ForThatOwnerOnly(string kind)
    {
        await Add(kind, _owner);

        Assert.True(await HasAny(_owner));
        Assert.False(await HasAny(_other));
        Assert.False(await HasAny(_guest));
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("bond")]
    [InlineData("trade")]
    [InlineData("dividend")]
    public async Task AnotherOwnersRows_DoNotCount(string kind)
    {
        await Add(kind, _other);

        Assert.False(await HasAny(_owner));
        Assert.True(await HasAny(_other));
    }

    [Theory]
    [InlineData("cash")]
    [InlineData("bond")]
    [InlineData("trade")]
    [InlineData("dividend")]
    public async Task NegativeGuestId_IsReadLikeAnyOtherOwner(string kind)
    {
        await Add(kind, _guest);

        Assert.True(await HasAny(_guest));
        Assert.False(await HasAny(_owner));
    }

    [Fact]
    public async Task UserIdZero_IsRejected() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.HasAny(0, Token));
}