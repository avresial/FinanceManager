using FinanceManager.Application.Identity.RefreshTokens;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class RefreshTokenServiceTests
{
    private readonly Mock<IRefreshTokenRepository> _repository = new();
    private readonly RefreshTokenOptions _options = new() { SlidingValidityDays = 14, AbsoluteValidityDays = 30 };
    private readonly RefreshTokenService _service;

    public RefreshTokenServiceTests() =>
        _service = new RefreshTokenService(_repository.Object, Options.Create(_options));

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    [Fact]
    public async Task Issue_PersistsHashedToken_AndReturnsRawToken()
    {
        RefreshToken? stored = null;
        _repository.Setup(r => r.Add(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => stored = t)
            .Returns(Task.CompletedTask);

        var raw = await _service.Issue(42, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(raw));
        Assert.NotNull(stored);
        Assert.Equal(42, stored!.UserId);
        // The raw token must never be persisted — only its hash.
        Assert.NotEqual(raw, stored.TokenHash);
        Assert.Equal(Hash(raw), stored.TokenHash);
        Assert.NotEqual(Guid.Empty, stored.FamilyId);
        Assert.True(stored.ExpiresAt > DateTime.UtcNow.AddDays(13));
        Assert.True(stored.AbsoluteExpiresAt > DateTime.UtcNow.AddDays(29));
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    public async Task ValidateAndRotate_Success_RevokesOldAndIssuesNew()
    {
        var now = DateTime.UtcNow;
        var existing = new RefreshToken
        {
            Id = 1,
            UserId = 7,
            TokenHash = Hash("anything"),
            FamilyId = Guid.NewGuid(),
            CreatedAt = now.AddMinutes(-5),
            ExpiresAt = now.AddDays(10),
            AbsoluteExpiresAt = now.AddDays(20),
        };
        _repository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync(existing);

        RefreshToken? revoked = null;
        RefreshToken? added = null;
        _repository.Setup(r => r.Rotate(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>()))
            .Callback<RefreshToken, RefreshToken>((rev, rep) => { revoked = rev; added = rep; })
            .Returns(Task.CompletedTask);

        var result = await _service.ValidateAndRotate("anything", TestContext.Current.CancellationToken);

        Assert.Equal(RefreshTokenStatus.Success, result.Status);
        Assert.Equal(7, result.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.NewRefreshToken));

        // Old token revoked and linked to its replacement, committed atomically with the replacement insert.
        Assert.NotNull(existing.RevokedAt);
        Assert.Equal(Hash(result.NewRefreshToken!), existing.ReplacedByTokenHash);
        _repository.Verify(r => r.Rotate(existing, It.IsAny<RefreshToken>()), Times.Once);
        Assert.Same(existing, revoked);

        // New token shares the family and keeps the absolute cap.
        Assert.NotNull(added);
        Assert.Equal(existing.FamilyId, added!.FamilyId);
        Assert.Equal(existing.AbsoluteExpiresAt, added.AbsoluteExpiresAt);
        Assert.Equal(7, added.UserId);
    }

    [Fact]
    public async Task ValidateAndRotate_NewExpiry_NeverExceedsAbsoluteCap()
    {
        var now = DateTime.UtcNow;
        // Family is near its absolute cap, so the sliding extension must be clamped to it.
        var existing = new RefreshToken
        {
            UserId = 7,
            TokenHash = Hash("x"),
            FamilyId = Guid.NewGuid(),
            CreatedAt = now.AddDays(-29),
            ExpiresAt = now.AddDays(1),
            AbsoluteExpiresAt = now.AddDays(1),
        };
        _repository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync(existing);
        RefreshToken? added = null;
        _repository.Setup(r => r.Rotate(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>()))
            .Callback<RefreshToken, RefreshToken>((_, rep) => added = rep)
            .Returns(Task.CompletedTask);

        var result = await _service.ValidateAndRotate("x", TestContext.Current.CancellationToken);

        Assert.Equal(RefreshTokenStatus.Success, result.Status);
        Assert.NotNull(added);
        Assert.Equal(existing.AbsoluteExpiresAt, added!.ExpiresAt);
    }

    [Fact]
    public async Task ValidateAndRotate_UnknownToken_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        var result = await _service.ValidateAndRotate("ghost", TestContext.Current.CancellationToken);

        Assert.Equal(RefreshTokenStatus.NotFound, result.Status);
        _repository.Verify(r => r.Rotate(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAndRotate_ExpiredToken_ReturnsExpired_WithoutRotating()
    {
        var now = DateTime.UtcNow;
        _repository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync(new RefreshToken
        {
            UserId = 7,
            TokenHash = Hash("old"),
            FamilyId = Guid.NewGuid(),
            CreatedAt = now.AddDays(-15),
            ExpiresAt = now.AddMinutes(-1),
            AbsoluteExpiresAt = now.AddDays(15),
        });

        var result = await _service.ValidateAndRotate("old", TestContext.Current.CancellationToken);

        Assert.Equal(RefreshTokenStatus.Expired, result.Status);
        _repository.Verify(r => r.Rotate(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>()), Times.Never);
        _repository.Verify(r => r.RevokeFamily(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAndRotate_ReusedRevokedToken_RevokesEntireFamily()
    {
        var now = DateTime.UtcNow;
        var familyId = Guid.NewGuid();
        _repository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync(new RefreshToken
        {
            UserId = 7,
            TokenHash = Hash("reused"),
            FamilyId = familyId,
            CreatedAt = now.AddDays(-1),
            ExpiresAt = now.AddDays(10),
            AbsoluteExpiresAt = now.AddDays(20),
            RevokedAt = now.AddMinutes(-5),
        });

        var result = await _service.ValidateAndRotate("reused", TestContext.Current.CancellationToken);

        Assert.Equal(RefreshTokenStatus.Revoked, result.Status);
        _repository.Verify(r => r.RevokeFamily(familyId, It.IsAny<DateTime>()), Times.Once);
        _repository.Verify(r => r.Rotate(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task Revoke_ActiveToken_MarksRevoked()
    {
        var token = new RefreshToken
        {
            UserId = 7,
            TokenHash = Hash("live"),
            FamilyId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(20),
        };
        _repository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync(token);
        _repository.Setup(r => r.Update(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

        await _service.Revoke("live", TestContext.Current.CancellationToken);

        Assert.NotNull(token.RevokedAt);
        _repository.Verify(r => r.Update(token), Times.Once);
    }

    [Fact]
    public async Task Revoke_UnknownToken_DoesNothing()
    {
        _repository.Setup(r => r.GetByHash(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        await _service.Revoke("ghost", TestContext.Current.CancellationToken);

        _repository.Verify(r => r.Update(It.IsAny<RefreshToken>()), Times.Never);
    }
}