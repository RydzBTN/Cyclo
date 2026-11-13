using System.Security.Claims;
using Cyclo.Api.Data.Entities.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Cyclo.Api.Auth;

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    private readonly SigningCredentials _credentials =
        new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(options.Value.Key)), SecurityAlgorithms.HmacSha256);
    
    public AccessToken CreateToken(UserEntity user)
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.ExpiresMinutes);
        SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("role", user.Role.ToString())
            ])
        };

        string token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expiresAt);
    }
}