using FinanceManager.Application.Commands.Login;
using FinanceManager.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FinanceManager.Api.Services;

public partial class JwtTokenGenerator(IOptions<JwtAuthOptions> jwtOptions)
{
    public LoginResponseModel GenerateToken(string userName, int userId, UserRole userRole)
    {
        ArgumentNullException.ThrowIfNull(userName);

        var tokenExpiryTimeStamp = DateTime.UtcNow.AddMinutes(jwtOptions.Value.TokenValidityMins);

        var tokenDescriptor = new SecurityTokenDescriptor()
        {

            Subject = new(
            [
                new(JwtRegisteredClaimNames.Name, userName),
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, userRole.ToString()),
            ]),

            Expires = tokenExpiryTimeStamp,
            Issuer = jwtOptions.Value.Issuer,
            Audience = jwtOptions.Value.Audience,
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key)),
            SecurityAlgorithms.HmacSha512Signature),
        };

        JwtSecurityTokenHandler tokenHandler = new();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(securityToken);

        return new()
        {
            AccessToken = accessToken,
            UserName = userName,
            UserId = userId,
            UserRole = userRole,
            ExpiresIn = jwtOptions.Value.TokenValidityMins
        };
    }
}