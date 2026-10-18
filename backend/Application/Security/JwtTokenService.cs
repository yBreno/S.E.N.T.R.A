using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Interfaces.Security;
using Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace Application.Security;

public class JwtTokenService(IOptions<JwtSettings> options) : ITokenService
{
    private readonly JwtSettings _s = options.Value;

    public TokenResult GenerateToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_s.ExpiresMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_s.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_s.Issuer, _s.Audience, claims, expires: expires, signingCredentials: creds);
        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
