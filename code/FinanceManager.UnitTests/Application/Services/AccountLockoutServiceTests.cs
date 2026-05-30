using FinanceManager.Application.Options;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

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

    [Fact]
    public async Task RegisterFailedAttempt_BelowThreshold_IncrementsWithoutLocking()
    {
        SetupState(1, null);

        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.SetLoginThrottlingState(_login, 2, null), Times.Once);
    }

    [Fact]
    public async Task RegisterFailedAttempt_ReachingThreshold_LocksForConfiguredWindow()
    {
        SetupState(2, null);
        var before = DateTime.UtcNow;

        DateTime? capturedLockoutEnd = null;
        _userRepository.Setup(r => r.SetLoginThrottlingState(_login, It.IsAny<int>(), It.IsAny<DateTime?>()))
            .Callback<string, int, DateTime?>((_, _, end) => capturedLockoutEnd = end)
            .ReturnsAsync(true);

        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.SetLoginThrottlingState(_login, 3, It.IsAny<DateTime?>()), Times.Once);
        Assert.NotNull(capturedLockoutEnd);
        // Lockout end is roughly now + the configured duration.
        Assert.InRange(capturedLockoutEnd!.Value,
            before.Add(_options.LockoutDuration),
            DateTime.UtcNow.Add(_options.LockoutDuration).AddSeconds(1));
    }

    [Fact]
    public async Task RegisterFailedAttempt_UnknownAccount_IsIgnored()
    {
        _userRepository.Setup(r => r.GetLoginThrottlingState(_login)).ReturnsAsync((LoginThrottlingState?)null);

        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.SetLoginThrottlingState(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task RegisterFailedAttempt_AfterLockoutLapsed_StartsFreshCount()
    {
        // Threshold previously hit but the lockout window has already passed.
        SetupState(3, DateTime.UtcNow.AddMinutes(-1));

        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);

        // Counts from zero again rather than re-tripping the lock on the very next failure.
        _userRepository.Verify(r => r.SetLoginThrottlingState(_login, 1, null), Times.Once);
    }

    [Fact]
    public async Task IsLockedOut_WhenLockoutInFuture_ReturnsTrue()
    {
        SetupState(3, DateTime.UtcNow.AddMinutes(5));

        Assert.True(await _service.IsLockedOut(_login, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsLockedOut_WhenLockoutExpired_ReturnsFalse()
    {
        SetupState(3, DateTime.UtcNow.AddMinutes(-5));

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

        _userRepository.Verify(r => r.SetLoginThrottlingState(_login, 0, null), Times.Once);
    }

    [Fact]
    public async Task Reset_WhenAlreadyClean_DoesNotWrite()
    {
        SetupState(0, null);

        await _service.Reset(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.SetLoginThrottlingState(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime?>()), Times.Never);
    }

    [Fact]
    public async Task Disabled_ShortCircuitsAllOperations()
    {
        _options.Enabled = false;
        SetupState(10, DateTime.UtcNow.AddMinutes(30));

        Assert.False(await _service.IsLockedOut(_login, TestContext.Current.CancellationToken));
        await _service.RegisterFailedAttempt(_login, TestContext.Current.CancellationToken);
        await _service.Reset(_login, TestContext.Current.CancellationToken);

        _userRepository.Verify(r => r.SetLoginThrottlingState(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime?>()), Times.Never);
    }
}