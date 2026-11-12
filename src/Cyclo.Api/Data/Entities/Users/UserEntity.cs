namespace Cyclo.Api.Data.Entities.Users;

public class UserEntity
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required Role Role { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
}