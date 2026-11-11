using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Lamort.Shared.Auth;
using Lamort.Shared.Web;
using Microsoft.IdentityModel.Tokens;

namespace Lamort.Web.Services;

public sealed class JwtTokenService(JwtSettings settings)
{
    public TokenResponse CreateToken(LamortUser user)
    {
        var expiresUtc = DateTime.UtcNow.AddMinutes(settings.ExpirationMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expiresUtc,
            signingCredentials: credentials);

        return new TokenResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresUtc = expiresUtc
        };
    }
}
