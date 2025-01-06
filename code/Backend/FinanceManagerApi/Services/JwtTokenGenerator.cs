﻿using FinanceManagerApi.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FinanceManagerApi.Services
{
    public class JwtTokenGenerator
    {
        private readonly IConfiguration configuration;

        public JwtTokenGenerator(IConfiguration configuration)
        {
            this.configuration = configuration;
        }


        public LoginResponseModel GenerateToken(LoginRequestModel loginRequestModel)
        {
            if (string.IsNullOrEmpty(loginRequestModel.UserName) || string.IsNullOrEmpty(loginRequestModel.Password)) return null;

            // Check user validity with database

            var claims = new List<Claim>()
            {
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Sub, loginRequestModel.UserName),
            };

            var tokenValidityInMins = configuration.GetValue<int>("JwtConfig:TokenValidityMins");
            var tokenExpiryTimeStamp = DateTime.UtcNow.AddMinutes(tokenValidityInMins);

            var tokenDescriptor = new SecurityTokenDescriptor()
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(JwtRegisteredClaimNames.Name, loginRequestModel.UserName)
                }),
                Expires = tokenExpiryTimeStamp,
                Issuer = configuration["JwtConfig:Issuer"],
                Audience = configuration["JwtConfig:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtConfig:Key"]!)),
                SecurityAlgorithms.HmacSha512Signature),
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            var accessToken = tokenHandler.WriteToken(securityToken);

            return new LoginResponseModel()
            {
                AccessToken = accessToken,
                UserName = loginRequestModel.UserName,
                ExpiresIn = tokenValidityInMins
            };
        }
    }
}
