namespace Cyclo.Api.Auth;

public record AccessToken(string Token, DateTimeOffset ExpiresAt);