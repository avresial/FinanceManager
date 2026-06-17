using FinanceManager.Application.Identity.Lockout;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceManager.Tests.Integration.Repositories;

// Exercises the login-throttling repository methods against the in-memory provider (the read-modify-write fallback
// used whenever the database is non-relational). The relational atomic-increment path is covered implicitly by the
// shared logic; here we assert the persisted semantics the AccountLockoutService relies on.
[Trait("Category", "Integration")]
public sealed class UserRepositoryThrottlingTests : IDisposable
{
    private const string _login = "throttleuser";

    private readonly AppDbContext _context;
    private readonly UserRepository _repository;

    public UserRepositoryThrottlingTests()
    {
        _context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);
        _repository = new UserRepository(_context);
    }

    private Task SeedUser() => _repository.AddUser(_login, "pwd", PricingLevel.Free, UserRole.User);

    [Fact]
    public async Task IncrementFailedLoginAttempts_AccumulatesAndPersists()
    {
        await SeedUser();

        Assert.Equal(1, await _repository.IncrementFailedLoginAttempts(_login));
        Assert.Equal(2, await _repository.IncrementFailedLoginAttempts(_login));
        Assert.Equal(3, await _repository.IncrementFailedLoginAttempts(_login));

        var state = await _repository.GetLoginThrottlingState(_login);
        Assert.NotNull(state);
        Assert.Equal(3, state!.FailedAttempts);
        Assert.Null(state.LockoutEndUtc);
    }

    [Fact]
    public async Task IncrementFailedLoginAttempts_UnknownAccount_ReturnsNull()
    {
        Assert.Null(await _repository.IncrementFailedLoginAttempts("ghost"));
    }

    [Fact]
    public async Task LockAccount_SetsLockoutAndClearsCounter()
    {
        await SeedUser();
        await _repository.IncrementFailedLoginAttempts(_login);
        await _repository.IncrementFailedLoginAttempts(_login);

        var lockoutEnd = DateTime.UtcNow.AddMinutes(15);
        Assert.True(await _repository.LockAccount(_login, lockoutEnd));

        var state = await _repository.GetLoginThrottlingState(_login);
        Assert.NotNull(state);
        Assert.Equal(0, state!.FailedAttempts);
        Assert.Equal(lockoutEnd, state.LockoutEndUtc);
    }

    [Fact]
    public async Task ResetLoginThrottling_ClearsCounterAndLockout()
    {
        await SeedUser();
        await _repository.IncrementFailedLoginAttempts(_login);
        await _repository.LockAccount(_login, DateTime.UtcNow.AddMinutes(15));

        Assert.True(await _repository.ResetLoginThrottling(_login));

        var state = await _repository.GetLoginThrottlingState(_login);
        Assert.NotNull(state);
        Assert.Equal(0, state!.FailedAttempts);
        Assert.Null(state.LockoutEndUtc);
    }

    [Fact]
    public async Task LockAndReset_UnknownAccount_ReturnFalse()
    {
        Assert.False(await _repository.LockAccount("ghost", DateTime.UtcNow.AddMinutes(15)));
        Assert.False(await _repository.ResetLoginThrottling("ghost"));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}