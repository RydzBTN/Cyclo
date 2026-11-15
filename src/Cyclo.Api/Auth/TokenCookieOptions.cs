namespace Cyclo.Api.Auth;

public static class TokenCookieOptions
{
    public const string AccessTokenCookieName = "token";
    
    public static CookieOptions GetBaseTokenCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api"
        };
    }
}