using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using RSSVibe.ApiService.Configuration;
using RSSVibe.ApiService.Endpoints;
using RSSVibe.Contracts.Auth;
using RSSVibe.Data;
using RSSVibe.Data.Entities;
using RSSVibe.Data.Extensions;
using RSSVibe.Services.Auth;
using RSSVibe.Services.Extensions;
using Scalar.AspNetCore;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRssVibeDatabase("rssvibedb");

// Add Identity with SignInManager support
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<RssVibeDbContext>();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

// Configure authentication settings
builder.Services.Configure<AuthConfiguration>(
    builder.Configuration.GetSection("Auth"));

// Configure JWT settings
builder.Services.Configure<JwtConfiguration>(
    builder.Configuration.GetSection("Jwt"));

// Add authorization services configured from JwtConfiguration
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtConfiguration>()
                 ?? throw new InvalidOperationException("Jwt configuration is missing");

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(config =>
{
    config.TokenValidationParameters.LogValidationExceptions = true;
    config.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));
    config.TokenValidationParameters.ValidateIssuer = true;
    config.TokenValidationParameters.ValidateAudience = true;
    config.TokenValidationParameters.ValidIssuer = jwtOptions.Issuer;
    config.TokenValidationParameters.ValidAudience = jwtOptions.Audience;
});
builder.Services.AddAuthorization();

// Configure Identity password and lockout policy
builder.Services.Configure<IdentityOptions>(options =>
{
    // Password requirements
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;

    // Lockout settings - protect against brute force attacks
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
});

// Register all RSSVibe application services
builder.Services.AddRssVibeServices();

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequest>();
builder.Services.AddFluentValidationAutoValidation();

// Configure rate limiting to prevent brute force attacks on password operations
// Skip rate limiting in integration tests to avoid test failures
if (!builder.Environment.IsIntegrationTests())
{
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("password-change", httpContext =>
        {
            // Simple per-user rate limiting using IP + User ID as key
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var key = $"{userId}:{clientIp}";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: key,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15)
                });
        });
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Only enable rate limiter middleware if rate limiting is configured
if (!app.Environment.IsIntegrationTests())
{
    app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Map all API endpoints organized hierarchically under /api/v1
app.MapApiV1();

app.MapDefaultEndpoints();

await app.RunAsync();

// This is neccesary for integration tests to work, can be removed in .NET 10
// ReSharper disable once ClassNeverInstantiated.Global
public partial class Program;
