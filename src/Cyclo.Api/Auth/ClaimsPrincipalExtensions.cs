using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Cyclo.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        string? idString = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (idString == null || !Guid.TryParse(idString, out Guid id))
            throw new InvalidOperationException("Authenticated user has no valid 'sub' claim");
        
        return id;
    }
}