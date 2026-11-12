using System.ComponentModel.DataAnnotations;

namespace Cyclo.Api.Contracts.Auth;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(255)]
    string Email,
    [Required, MinLength(8), MaxLength(255)]
    string Password
    );