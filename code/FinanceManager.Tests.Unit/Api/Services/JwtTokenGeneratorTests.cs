using FinanceManager.Api.Services;
using FinanceManager.Application.Identity;
using FinanceManager.Domain.Identity.Entities;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FinanceManager.Tests.Unit.Api.Services;

[Trait("Category", "Unit")]
public class JwtTokenGeneratorTests
{
    [Theory]
    [InlineData(UserRole.User, false)]
    [InlineData(UserRole.Admin, true)]
    public async Task Role_IsAppliedToServerTokenAndClientAuthenticationState(UserRole role, bool isAdmin)
    {
        var generator = new JwtTokenGenerator(Options.Create(new JwtAuthOptions
        {
            TokenValidityMins = 15,
            Issuer = "test",
            Audience = "test",
            Key = new string('k', 64),
        }));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(generator.GenerateToken("user", 1, role).AccessToken);
        var tokenRoles = token.Claims.Where(claim => claim.Type is ClaimTypes.Role or "role").Select(claim => claim.Value).ToList();

        var provider = new CustomAuthenticationStateProvider();
        var state = await provider.ChangeUser("user", "1", role);

        string[] expectedRoles = isAdmin ? [nameof(UserRole.User), nameof(UserRole.Admin)] : [nameof(UserRole.User)];
        Assert.Equal(expectedRoles, tokenRoles);
        Assert.True(state.User.IsInRole(nameof(UserRole.User)));
        Assert.Equal(isAdmin, state.User.IsInRole(nameof(UserRole.Admin)));
    }
}