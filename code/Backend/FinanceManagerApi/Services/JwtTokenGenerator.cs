using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FinanceManagerApi.Services
{
    public class JwtTokenGenerator
    {
        public string GenerateToken(Guid userId, string login)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = "testkey"u8.ToArray();

            var claims = new List<Claim>()
            {
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            };

            var tokenDescriptor

            return "";
        }
    }
}
