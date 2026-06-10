using FinanceManager.Application.Identity.Lockout;
using FinanceManager.Application.Services;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class AccountLockoutServiceTests
{
    private const string _login = "alice";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly AccountLockoutOptions _options = new()
    {
        Enabled = true,
        MaxFailedAttempts = 3,
        LockoutDuration = TimeSpan.FromMinutes(15),
    };
    private readonly AccountLockoutService _service;

    public AccountLockoutServiceTests() =>
        _service = new AccountLockoutService(_userRepository.Object, Options.Create(_options));

    private void SetupState(int failedAttempts, DateTime? lockoutEndUtc) =>
        _userRepository.Setup(r => r.GetLoginThrottlingState(_login))
            .ReturnsAsync(new LoginThrottlingState(failedAttempts, lockoutEndUtc));

    private void SetupIncrementReturns(int newCount) =>
        _userRepository.Setup(r => r.IncrementFailedLoginAttempts(_login)).ReturnsAsync(newCount);

    [Fact]
    public async Task RegisterFailedAttempt_BelowThreshold_IncrementsWithoutLocking()
    {
        SetupIncrementReturns(2);

        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.IncrementFailedLoginAttempts(_login), Times.Once);
        _userRepository.Verify(r => r.LockAccount(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RegisterFailedAttempt_ReachingThreshold_LocksForConfiguredWindow()
    {
        SetupIncrementReturns(3);
        var before = DateTime.UtcNow;

        DateTime capturedLockoutEnd = default;
        _userRepository.Setup(r => r.LockAccount(_login, It.IsAny<DateTime>()))
            .Callback<string, DateTime>((_, end) => capturedLockoutEnd = end)
            .ReturnsAsync(true);

        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.LockAccount(_login, It.IsAny<DateTime>()), Times.Once);
        // Lockout end is roughly now + the configured duration.
        Assert.InRange(capturedLockoutEnd,
            before.Add(_options.LockoutDuration),
            DateTime.UtcNow.Add(_options.LockoutDuration).AddSeconds(1));
    }

    [Fact]
    public async Task RegisterFailedAttempt_ExceedingThreshold_StillLocks()
    {
        // A concurrent increment can push the count past the threshold; that must still lock (the safe direction).
        SetupIncrementReturns(7);

        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.LockAccount(_login, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task RegisterFailedAttempt_UnknownAccount_IsIgnored()
    {
        _userRepository.Setup(r => r.IncrementFailedLoginAttempts(_login)).ReturnsAsync((int?)null);

        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.LockAccount(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task IsLockedOut_WhenLockoutInFuture_ReturnsTrue()
    {
        SetupState(0, DateTime.UtcNow.AddMinutes(5));

        Assert.True(await _service.IsLockedOut(_login, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsLockedOut_WhenLockoutExpired_ReturnsFalse()
    {
        SetupState(0, DateTime.UtcNow.AddMinutes(-5));

        Assert.False(await _service.IsLockedOut(_login, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsLockedOut_WhenNoLockout_ReturnsFalse()
    {
        SetupState(1, null);

        Assert.False(await _service.IsLockedOut(_login, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsLockedOut_UnknownAccount_ReturnsFalse()
    {
        _userRepository.Setup(r => r.GetLoginThrottlingState(_login)).ReturnsAsync((LoginThrottlingState?)null);

        Assert.False(await _service.IsLockedOut(_login, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reset_WithExistingFailures_ClearsCounterAndLockout()
    {
        SetupState(3, DateTime.UtcNow.AddMinutes(5));

        await _service.Reset(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.ResetLoginThrottling(_login), Times.Once);
    }

    [Fact]
    public async Task Reset_WhenAlreadyClean_DoesNotWrite()
    {
        SetupState(0, null);

        await _service.Reset(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.ResetLoginThrottling(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Disabled_ShortCircuitsAllOperations()
    {
        _options.Enabled = false;
        SetupState(10, DateTime.UtcNow.AddMinutes(30));
        SetupIncrementReturns(11);

        Assert.False(await _service.IsLockedOut(_login, TestContext.Current.CancellationToken));
        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);
        await _service.Reset(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.IncrementFailedLoginAttempts(It.IsAny<string>()), Times.Never);
        _userRepository.Verify(r => r.LockAccount(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        _userRepository.Verify(r => r.ResetLoginThrottling(It.IsAny<string>()), Times.Never);
    }
}