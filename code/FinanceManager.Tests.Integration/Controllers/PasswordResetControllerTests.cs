using FinanceManager.Application.Commands.User;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class PasswordResetControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private const string _login = "reset.user@example.com";
    private const int _userId = 7777;

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
        services.AddSingleton<IPasswordResetTokenRepository, InMemoryPasswordResetTokenRepository>();
    }

    [Fact]
    public async Task ForgotPassword_KnownUser_ReturnsMessageAndToken()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest(_login), ct);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>(cancellationToken: ct);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
        // While no email provider is wired up the token rides back on the response so the UI can show a link.
        Assert.False(string.IsNullOrWhiteSpace(body.ResetToken));
    }

    [Fact]
    public async Task ForgotPassword_KnownAndUnknownUser_ReturnIndistinguishableResponses()
    {
        var ct = TestContext.Current.CancellationToken;

        var known = await Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest(_login), ct);
        var unknown = await Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest("ghost@example.com"), ct);

        var knownBody = await known.Content.ReadFromJsonAsync<ForgotPasswordResponse>(cancellationToken: ct);
        var unknownBody = await unknown.Content.ReadFromJsonAsync<ForgotPasswordResponse>(cancellationToken: ct);

        Assert.NotNull(knownBody);
        Assert.NotNull(unknownBody);
        // No account-enumeration signal: identical status, message, and response shape (both carry a token) for both.
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(knownBody.Message, unknownBody.Message);
        Assert.False(string.IsNullOrWhiteSpace(knownBody.ResetToken));
        Assert.False(string.IsNullOrWhiteSpace(unknownBody.ResetToken));
    }

    [Fact]
    public async Task ResetPassword_WithTokenFromUnknownUser_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;

        // The throwaway token handed back for an unregistered email is never persisted, so it can't reset anything.
        var forgot = await Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest("ghost@example.com"), ct);
        var forgotBody = await forgot.Content.ReadFromJsonAsync<ForgotPasswordResponse>(cancellationToken: ct);
        Assert.False(string.IsNullOrWhiteSpace(forgotBody!.ResetToken));

        var reset = await Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest(forgotBody.ResetToken!, "brand-new-password"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithIssuedToken_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;

        var forgot = await Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest(_login), ct);
        var forgotBody = await forgot.Content.ReadFromJsonAsync<ForgotPasswordResponse>(cancellationToken: ct);
        Assert.NotNull(forgotBody?.ResetToken);

        var reset = await Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest(forgotBody!.ResetToken!, "brand-new-password"), ct);

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

        var forgot = await Client.PostAsJsonAsync("api/PasswordReset/forgot-password", new ForgotPasswordRequest(_login), ct);
        var forgotBody = await forgot.Content.ReadFromJsonAsync<ForgotPasswordResponse>(cancellationToken: ct);
        var token = forgotBody!.ResetToken!;

        var first = await Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest(token, "first-password"), ct);
        var second = await Client.PostAsJsonAsync("api/PasswordReset/reset-password",
            new ResetPasswordRequest(token, "second-password"), ct);

        first.EnsureSuccessStatusCode();
        // Single-use: the consumed token can't be redeemed again.
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
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