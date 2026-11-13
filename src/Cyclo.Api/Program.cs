using Cyclo.Api.Auth;
using Cyclo.Api.Data;
using Cyclo.Api.Data.Entities.Users;
using Cyclo.Api.Processing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
builder.Services.AddSingleton<DummyPasswordHash>();
builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.IncludeErrorDetails = builder.Environment.IsDevelopment();
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.Value.Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

builder.Services.AddOptions<ActivityQueueOptions>()
    .BindConfiguration(ActivityQueueOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<ActivityQueue>();

string connectionString = builder.Configuration.GetConnectionString("Cyclo") ??
                          throw new InvalidOperationException("no connection string");
builder.Services.AddDbContext<CycloDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddHostedService<ActivityProcessingService>();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
