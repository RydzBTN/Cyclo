namespace Cyclo.Api.Contracts.Auth;

public record CurrentUserResponse(string Email, DateTimeOffset ExpiresAt);