using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.IdentityModel.Tokens;
using PassengerManager.Shared.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PassengerManager.Server.Services.Security
{
    public interface ITokenService
    {
        string GenerateIdToken(Shared.Models.User user);

        string GenerateDriverToken(Shared.Models.User user, long shiftId, string vehicleId);
    }

    public class JwtTokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly SymmetricSecurityKey _key;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
            _key = new SymmetricSecurityKey(System.Text.Encoding.UTF8
                .GetBytes(_configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JWT secret key is not configured.")));
        }

        private string CreateToken(List<Claim> claims, double expiryMinutes)
        {
            SigningCredentials creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _configuration["JwtSettings:Issuer"],
                audience: _configuration["JwtSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: creds
                );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateIdToken(Shared.Models.User user)
        {
            if (user.Role == null)
            {
                throw new InvalidOperationException("Cannot generate token: User.Role is null");
            }

            List<Claim> claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, user.Role.RoleName),
                new Claim("AccessLevel", user.Role.AccessLevel.ToString())
            };

            return CreateToken(claims, _configuration.GetValue<double>("JswSettings:GeneralExpiryMinutes"));
        }

        public string GenerateDriverToken(Shared.Models.User user, long shiftId, string vehicleId)
        {
            if (user.Role == null)
            {
                throw new InvalidOperationException("Cannot generate token: User.Role is null");
            }

            List<Claim> claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, user.Role.RoleName),
                new Claim("AccessLevel", user.Role.AccessLevel.ToString()),
                new Claim("ShiftId", shiftId.ToString()),
                new Claim("VehicleId", vehicleId)
            };
            return CreateToken(claims, _configuration.GetValue<double>("JwtSettings:DriverExpiryMinutes"));
        }
    }
}
