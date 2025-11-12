using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using RSSVibe.ApiService.Configuration;
using RSSVibe.ApiService.Endpoints;
using RSSVibe.ApiService.Middleware;
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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var clients = builder.Configuration.GetServiceEndpoints("frontend");

        policy.WithOrigins(clients);
        policy.AllowAnyMethod();
        policy.AllowAnyHeader();
        // AllowCredentials is required for cookie-based authentication
        policy.AllowCredentials();
    });
});

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

    // Extract JWT from Authorization header OR from access_token cookie
    // This allows both Bearer token and cookie-based authentication to work
    config.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // First try Authorization header (Bearer token)
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = authHeader["Bearer ".Length..];
                return Task.CompletedTask;
            }

            // Fall back to access_token cookie if header not found
            if (context.Request.Cookies.TryGetValue("access_token", out var token))
            {
                context.Token = token;
            }

            return Task.CompletedTask;
        }
    };
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

// Configure HttpClient for preflight checks
builder.Services.AddHttpClient("PreflightClient")
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    });

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
app.UseCors();

// Only enable rate limiter middleware if rate limiting is configured
if (!app.Environment.IsIntegrationTests())
{
    app.UseRateLimiter();
}

// JWT refresh middleware must run BEFORE authentication
// It checks if tokens are expired and refreshes them transparently before auth
app.UseJwtRefresh();

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

// Create test user
if (!app.Environment.IsIntegrationTests())
{
// TODO: Parametrize this in the future
    using (var scope = app.Services.CreateScope())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var testUser = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "TestUser",
            Email = "test@test.com",
            DisplayName = "Test User",
            MustChangePassword = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var existingUser = await userManager.FindByEmailAsync(testUser.Email);

        if (existingUser is null)
        {
            var createResult = await userManager.CreateAsync(testUser, "P@ssw0rd1234");
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create test user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}

await app.RunAsync();

// This is neccesary for integration tests to work, can be removed in .NET 10
// ReSharper disable once ClassNeverInstantiated.Global
public partial class Program;
