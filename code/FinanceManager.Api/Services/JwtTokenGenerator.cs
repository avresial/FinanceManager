using FinanceManager.Application.Commands.Login;
using FinanceManager.Domain.Enums;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FinanceManager.Api.Services;

public class JwtTokenGenerator(IConfiguration configuration)
{
    public LoginResponseModel? GenerateToken(string userName, int userId, UserRole userRole)
    {
        if (string.IsNullOrEmpty(userName)) return null;

        // Check user validity with database

        var tokenValidityInMins = configuration.GetValue<int>("JwtConfig:TokenValidityMins");
        var tokenExpiryTimeStamp = DateTime.UtcNow.AddMinutes(tokenValidityInMins);

        var tokenDescriptor = new SecurityTokenDescriptor()
        {

            Subject = new(
            [
                new(JwtRegisteredClaimNames.Name, userName),
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, userRole.ToString()),
            ]),

            Expires = tokenExpiryTimeStamp,
            Issuer = configuration["JwtConfig:Issuer"],
            Audience = configuration["JwtConfig:Audience"],
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtConfig:Key"]!)),
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
            ExpiresIn = tokenValidityInMins
        };
    }
}