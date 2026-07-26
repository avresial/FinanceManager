using FinanceManager.Api.Features.Identity.Services;
using FinanceManager.Api.Features.Mcp.OAuth;
using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Features.Mcp.OAuth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FinanceManager.Tests.Unit.Api.Features.Mcp.OAuth;

public sealed class McpOAuthSecurityTests
{
    private const string _issuer = "FinanceManager.api";
    private const string _audience = "FinanceManager.Frontend";
    private const string _key = "unit-test-only-insecure-jwt-signing-key-not-for-production-use-123456";

    [Theory]
    [InlineData("https://finance.example/connect/authorize?client_id=client", true)]
    [InlineData("https://evil.example/connect/authorize", false)]
    [InlineData("http://finance.example/connect/authorize", false)]
    [InlineData("https://finance.example/connect/token", false)]
    [InlineData("https://finance.example/connect/authorize#fragment", false)]
    [InlineData("/connect/authorize", false)]
    public void ReturnUrlValidator_AllowsOnlyTheIssuerAuthorizationEndpoint(string returnUrl, bool expected)
    {
        var options = new McpOAuthOptions { Issuer = "https://finance.example/" };

        Assert.Equal(expected, McpOAuthReturnUrlValidator.IsValid(returnUrl, options));
    }

    [Fact]
    public void BridgeTokenValidator_AcceptsValidFinanceManagerToken()
    {
        var validator = CreateValidator();
        var token = new JwtTokenGenerator(Options.Create(JwtOptions()))
            .GenerateToken("mcp@example.com", 42, UserRole.User)
            .AccessToken;

        var principal = validator.Validate(token);

        Assert.NotNull(principal);
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public void BridgeTokenValidator_RejectsExpiredToken()
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "42")]),
            Issuer = _issuer,
            Audience = _audience,
            NotBefore = DateTime.UtcNow.AddMinutes(-10),
            Expires = DateTime.UtcNow.AddMinutes(-5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                SecurityAlgorithms.HmacSha512Signature)
        };
        var handler = new JwtSecurityTokenHandler();
        var token = handler.WriteToken(handler.CreateToken(descriptor));

        Assert.Null(CreateValidator().Validate(token));
    }

    private static McpOAuthBridgeTokenValidator CreateValidator()
    {
        var monitor = new Mock<IOptionsMonitor<JwtBearerOptions>>();
        monitor.Setup(options => options.Get(JwtBearerDefaults.AuthenticationScheme)).Returns(new JwtBearerOptions
        {
            TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            }
        });
        return new McpOAuthBridgeTokenValidator(monitor.Object);
    }

    private static JwtAuthOptions JwtOptions() => new()
    {
        Issuer = _issuer,
        Audience = _audience,
        Key = _key,
        TokenValidityMins = 15
    };
}