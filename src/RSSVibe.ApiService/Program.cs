using FluentValidation;
using Microsoft.AspNetCore.Identity;
using RSSVibe.ApiService.Configuration;
using RSSVibe.ApiService.Endpoints;
using RSSVibe.Contracts.Auth;
using RSSVibe.Data.Extensions;
using RSSVibe.Services.Extensions;
using Scalar.AspNetCore;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRssVibeDatabase("rssvibedb");

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

// Configure authentication settings
builder.Services.Configure<AuthConfiguration>(
    builder.Configuration.GetSection("Auth"));

// Configure Identity password policy
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
});

// Register all RSSVibe application services
builder.Services.AddRssVibeServices();

builder.Services.AddValidatorsFromAssemblyContaining<LoginRequest>();
builder.Services.AddFluentValidationAutoValidation();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

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
