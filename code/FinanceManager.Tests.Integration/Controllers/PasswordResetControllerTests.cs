using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Commands;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class PasswordResetControllerTests : ControllerTests
{
    private const string _login = "reset.user@example.com";
    private const int _userId = 7777;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private InMemoryPasswordResetTokenRepository? _tokenRepository;

    public PasswordResetControllerTests(OptionsProvider optionsProvider)
        : base(optionsProvider, Environments.Development)
    {
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(x => x.GetUser(_login)).ReturnsAsync(new User
        {
            UserId = _userId,
            Login = _login,
            UserRole = UserRole.User,
            CreationDate = DateTime.UtcNow,
        });
        userRepoMock.Setup(x => x.UpdatePassword(_userId, It.IsAny<string>())).ReturnsAsync(true);
        services.AddSingleton(userRepoMock.Object);

        // A real-but-in-memory token store so the full controller → service → repository flow runs without depending
        // on a configured database provider.
        _tokenRepository = new InMemoryPasswordResetTokenRepository();
        services.AddSingleton<IPasswordResetTokenRepository>(_tokenRepository);
    }

    [Fact]
    public async Task PasswordResetEndpoints_OutsideDevelopment_ReturnNotImplemented()
    {
        var ct = TestContext.Current.CancellationToken;
        using var app = new FinanceManagerApiTestApp(ConfigureServices);

        var forgot = await app.Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest(_login), ct);
        var reset = await app.Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest("stolen-token", "brand-new-password"), ct);
        var forgotBody = await forgot.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.NotImplemented, forgot.StatusCode);
        Assert.Equal(HttpStatusCode.NotImplemented, reset.StatusCode);
        Assert.DoesNotContain("resetToken", forgotBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPassword_KnownUser_ReturnsMessageWithoutTokenField()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest(_login), ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<ForgotPasswordResponse>(json, _jsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
        Assert.DoesNotContain("resetToken", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPassword_DoesNotReturnRawTokenFromService()
    {
        const string rawToken = "known-raw-token-from-service";
        var ct = TestContext.Current.CancellationToken;
        using var app = new FinanceManagerApiTestApp(services =>
        {
            ConfigureServices(services);
            services.AddSingleton<IPasswordResetService>(new StubPasswordResetService(rawToken));
        }, Environments.Development);

        var response = await app.Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest(_login), ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain(rawToken, json, StringComparison.Ordinal);
        Assert.DoesNotContain("resetToken", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPassword_KnownAndUnknownUser_ReturnIndistinguishableResponsesWithoutTokens()
    {
        var ct = TestContext.Current.CancellationToken;

        var known = await Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest(_login), ct);
        var unknown = await Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest("ghost@example.com"), ct);

        var knownBody = await known.Content.ReadAsStringAsync(ct);
        var unknownBody = await unknown.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(knownBody, unknownBody);
        Assert.DoesNotContain("resetToken", knownBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resetToken", unknownBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetPassword_WithUnpersistedToken_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var reset = await Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest("unpersisted-token", "brand-new-password"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithIssuedToken_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        const string rawToken = "issued-token";

        await AddResetToken(rawToken);

        var reset = await Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest(rawToken, "brand-new-password"), ct);

        reset.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;

        var reset = await Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest("not-a-real-token", "brand-new-password"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ReusedToken_IsRejectedOnSecondUse()
    {
        var ct = TestContext.Current.CancellationToken;
        const string rawToken = "single-use-token";

        await AddResetToken(rawToken);

        var first = await Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest(rawToken, "first-password"), ct);
        var second = await Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest(rawToken, "second-password"), ct);

        first.EnsureSuccessStatusCode();
        // Single-use: the consumed token can't be redeemed again.
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    private async Task AddResetToken(string rawToken)
    {
        await _tokenRepository!.Add(new PasswordResetToken
        {
            UserId = _userId,
            TokenHash = Hash(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
        });
    }

    private static string Hash(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private sealed class StubPasswordResetService(string rawToken) : IPasswordResetService
    {
        public Task<string> RequestReset(string login, CancellationToken cancellationToken = default) =>
            Task.FromResult(rawToken);

        public Task<PasswordResetResult> ResetPassword(
            string rawToken,
            string newPassword,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PasswordResetResult.Failure(PasswordResetStatus.InvalidToken));
    }

    private sealed class InMemoryPasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly List<PasswordResetToken> _tokens = [];
        private int _nextId = 1;

        public Task Add(PasswordResetToken token)
        {
            token.Id = _nextId++;
            _tokens.Add(token);
            return Task.CompletedTask;
        }

        public Task<PasswordResetToken?> GetByHash(string tokenHash) =>
            Task.FromResult(_tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task<bool> TryConsume(string tokenHash, DateTime usedAt)
        {
            var token = _tokens.FirstOrDefault(t => t.TokenHash == tokenHash && t.UsedAt is null);
            if (token is null) return Task.FromResult(false);

            token.UsedAt = usedAt;
            return Task.FromResult(true);
        }

        public Task InvalidateActiveTokensForUser(int userId, DateTime usedAt)
        {
            foreach (var token in _tokens.Where(t => t.UserId == userId && t.UsedAt is null))
                token.UsedAt = usedAt;
            return Task.CompletedTask;
        }

        public Task<int> RemoveExpired(DateTime utcNow)
        {
            var removed = _tokens.RemoveAll(t => t.ExpiresAt < utcNow);
            return Task.FromResult(removed);
        }
    }
}