using Cyclo.Api.Auth;
using Cyclo.Api.Data;
using Cyclo.Api.Data.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Cyclo.Api.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Cyclo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IPasswordHasher<UserEntity> hasher,
    CycloDbContext dbContext,
    DummyPasswordHash dummyHash,
    TokenService tokenService
    ) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        UserEntity user = new UserEntity
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = string.Empty,
            Role = Role.User,
            CreatedAt = DateTimeOffset.UtcNow
        };

        user.PasswordHash = hasher.HashPassword(user, request.Password);
        dbContext.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch(DbUpdateException ex) when (ex.InnerException is SqlException {Number: 2601 or 2627})
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "This user already exists");
        }

        return Created();
    }
    
    
    [HttpPost("login")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        string email = request.Email.Trim().ToLowerInvariant();
        
        UserEntity? user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        
        ObjectResult loginProblem = Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid email or password");
        if (user == null)
        {
            hasher.VerifyHashedPassword(dummyHash.User, dummyHash.Hash, request.Password);
            return loginProblem;
        }
        
        PasswordVerificationResult verificationResult =
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        switch (verificationResult)
        {
            case PasswordVerificationResult.SuccessRehashNeeded: 
                user.PasswordHash = hasher.HashPassword(user, request.Password);
                await dbContext.SaveChangesAsync(cancellationToken);
                break;
            case PasswordVerificationResult.Success:
                break;
            
            case PasswordVerificationResult.Failed:
                return loginProblem;
            default: 
                return loginProblem;
        }
        
        AccessToken token = tokenService.CreateToken(user);
        CookieOptions cookieOptions = TokenCookieOptions.GetBaseTokenCookieOptions();
        cookieOptions.Expires = token.ExpiresAt;
        Response.Cookies.Append(TokenCookieOptions.AccessTokenCookieName, token.Token, cookieOptions);
        
        return Ok(new CurrentUserResponse(user.Email, token.ExpiresAt));
    }
    
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> WhoAmI(CancellationToken cancellationToken)
    {
        Guid userId = User.GetUserId();
        DateTimeOffset expiresAt = User.GetExpiresAt();

        string? userEmail = await dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (userEmail == null) return Unauthorized();
        
        return Ok(new CurrentUserResponse(userEmail, expiresAt));
    }
    
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(TokenCookieOptions.AccessTokenCookieName, 
            TokenCookieOptions.GetBaseTokenCookieOptions());
        
        return NoContent();
    }
}












