using Cyclo.Api.Data.Entities.Users;
using Microsoft.AspNetCore.Identity;

namespace Cyclo.Api.Auth;

public class DummyPasswordHash
{
    public string Hash { get; }
    
    // todo: change to Verify(string passwd) method
    public readonly UserEntity User  = new UserEntity { Email = "", PasswordHash = "", Role = Role.User, CreatedAt = DateTimeOffset.MinValue };
    
    public DummyPasswordHash(IPasswordHasher<UserEntity> hasher)
    {
        Hash = hasher.HashPassword(User, "nieistniejącehaslo");
    }
}