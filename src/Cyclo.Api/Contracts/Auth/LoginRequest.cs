using System.ComponentModel.DataAnnotations;

namespace Cyclo.Api.Contracts.Auth;

public record LoginRequest(
    [Required]
    string Email,
    [Required, MaxLength(255)] 
    string Password
    );