using FinanceManager.Application.Identity;
using FinanceManager.Application.Identity.PasswordReset;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class PasswordResetServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordResetTokenRepository> _tokenRepository = new();
    private readonly PasswordResetOptions _options = new() { TokenValidityMinutes = 60 };
    private readonly PasswordResetService _service;

    public PasswordResetServiceTests() =>
        _service = new PasswordResetService(_userRepository.Object, _tokenRepository.Object, Options.Create(_options));

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static User BuildUser(int id = 42, string login = "user@example.com") => new()
    {
        UserId = id,
        Login = login,
        UserRole = UserRole.User,
        CreationDate = DateTime.UtcNow,
    };

    [Fact]
    public async Task RequestReset_KnownUser_PersistsHashedToken_AndReturnsRawToken()
    {
        _userRepository.Setup(r => r.GetUser("user@example.com")).ReturnsAsync(BuildUser());
        PasswordResetToken? stored = null;
        _tokenRepository.Setup(r => r.Add(It.IsAny<PasswordResetToken>()))
            .Callback<PasswordResetToken>(t => stored = t)
            .Returns(Task.CompletedTask);

        var raw = await _service.RequestReset("user@example.com", TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(raw));
        Assert.NotNull(stored);
        Assert.Equal(42, stored!.UserId);
        // The raw token must never be persisted — only its hash.
        Assert.NotEqual(raw, stored.TokenHash);
        Assert.Equal(Hash(raw!), stored.TokenHash);
        Assert.True(stored.ExpiresAt > DateTime.UtcNow.AddMinutes(59));
        Assert.Null(stored.UsedAt);
        // A new request supersedes any earlier active link.
        _tokenRepository.Verify(r => r.InvalidateActiveTokensForUser(42, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task RequestReset_NormalizesLoginCasing()
    {
        _userRepository.Setup(r => r.GetUser("user@example.com")).ReturnsAsync(BuildUser());
        _tokenRepository.Setup(r => r.Add(It.IsAny<PasswordResetToken>())).Returns(Task.CompletedTask);

        var raw = await _service.RequestReset("User@Example.com", TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(raw));
        _userRepository.Verify(r => r.GetUser("user@example.com"), Times.Once);
    }

    [Fact]
    public async Task RequestReset_UnknownUser_ReturnsThrowawayToken_WithoutPersisting()
    {
        _userRepository.Setup(r => r.GetUser(It.IsAny<string>())).ReturnsAsync((User?)null);

        var raw = await _service.RequestReset("ghost@example.com", TestContext.Current.CancellationToken);

        // A token is still returned so the response shape matches the known-account case (no enumeration signal)...
        Assert.False(string.IsNullOrWhiteSpace(raw));
        // ...but nothing is persisted, so the throwaway token can never reset an account.
        _tokenRepository.Verify(r => r.Add(It.IsAny<PasswordResetToken>()), Times.Never);
        _tokenRepository.Verify(r => r.InvalidateActiveTokensForUser(It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RequestReset_KnownAndUnknown_ReturnIndistinguishableTokens()
    {
        _userRepository.Setup(r => r.GetUser("user@example.com")).ReturnsAsync(BuildUser());
        _userRepository.Setup(r => r.GetUser("ghost@example.com")).ReturnsAsync((User?)null);
        _tokenRepository.Setup(r => r.Add(It.IsAny<PasswordResetToken>())).Returns(Task.CompletedTask);

        var known = await _service.RequestReset("user@example.com", TestContext.Current.CancellationToken);
        var unknown = await _service.RequestReset("ghost@example.com", TestContext.Current.CancellationToken);

        // Both branches return a non-empty token of the same shape, so the caller can't tell them apart.
        Assert.False(string.IsNullOrWhiteSpace(known));
        Assert.False(string.IsNullOrWhiteSpace(unknown));
        Assert.Equal(known!.Length, unknown!.Length);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_SetsPassword_AndConsumesToken()
    {
        var now = DateTime.UtcNow;
        var token = new PasswordResetToken
        {
            UserId = 7,
            TokenHash = Hash("valid"),
            CreatedAt = now.AddMinutes(-5),
            ExpiresAt = now.AddMinutes(30),
        };
        _tokenRepository.Setup(r => r.GetByHash(Hash("valid"))).ReturnsAsync(token);
        _tokenRepository.Setup(r => r.TryConsume(token.TokenHash, It.IsAny<DateTime>())).ReturnsAsync(true);
        _userRepository.Setup(r => r.UpdatePassword(7, It.IsAny<string>())).ReturnsAsync(true);

        var result = await _service.ResetPassword("valid", "newSecret", TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetStatus.Success, result.Status);
        Assert.True(result.Succeeded);
        // The token is claimed atomically before the password write.
        _tokenRepository.Verify(r => r.TryConsume(token.TokenHash, It.IsAny<DateTime>()), Times.Once);
        // Password is hashed before persistence — the raw value never reaches the repository.
        _userRepository.Verify(r => r.UpdatePassword(7, PasswordEncryptionProvider.EncryptPassword("newSecret")), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_WhenTokenClaimedConcurrently_ReturnsAlreadyUsed_WithoutChangingPassword()
    {
        var now = DateTime.UtcNow;
        var token = new PasswordResetToken
        {
            UserId = 7,
            TokenHash = Hash("valid"),
            CreatedAt = now.AddMinutes(-5),
            ExpiresAt = now.AddMinutes(30),
        };
        _tokenRepository.Setup(r => r.GetByHash(Hash("valid"))).ReturnsAsync(token);
        // A concurrent request won the atomic claim first, so this caller's consume affects no rows.
        _tokenRepository.Setup(r => r.TryConsume(token.TokenHash, It.IsAny<DateTime>())).ReturnsAsync(false);

        var result = await _service.ResetPassword("valid", "newSecret", TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetStatus.AlreadyUsed, result.Status);
        _userRepository.Verify(r => r.UpdatePassword(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_UnknownToken_ReturnsInvalid()
    {
        _tokenRepository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync((PasswordResetToken?)null);

        var result = await _service.ResetPassword("ghost", "newSecret", TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetStatus.InvalidToken, result.Status);
        _userRepository.Verify(r => r.UpdatePassword(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_ReturnsExpired_WithoutChangingPassword()
    {
        var now = DateTime.UtcNow;
        _tokenRepository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync(new PasswordResetToken
        {
            UserId = 7,
            TokenHash = Hash("old"),
            CreatedAt = now.AddHours(-2),
            ExpiresAt = now.AddMinutes(-1),
        });

        var result = await _service.ResetPassword("old", "newSecret", TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetStatus.Expired, result.Status);
        _userRepository.Verify(r => r.UpdatePassword(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_AlreadyUsedToken_ReturnsAlreadyUsed()
    {
        var now = DateTime.UtcNow;
        _tokenRepository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync(new PasswordResetToken
        {
            UserId = 7,
            TokenHash = Hash("used"),
            CreatedAt = now.AddMinutes(-10),
            ExpiresAt = now.AddMinutes(30),
            UsedAt = now.AddMinutes(-5),
        });

        var result = await _service.ResetPassword("used", "newSecret", TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetStatus.AlreadyUsed, result.Status);
        _userRepository.Verify(r => r.UpdatePassword(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_EmptyInput_ReturnsInvalid()
    {
        var result = await _service.ResetPassword("", "", TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetStatus.InvalidToken, result.Status);
        _tokenRepository.Verify(r => r.GetByHash(It.IsAny<string>()), Times.Never);
    }
}