using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using RSSVibe.Client;
using RSSVibe.Client.Services;
using RSSVibe.Contracts;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.AddServiceDefaults();
builder.Services.AddMudServices();

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Register localStorage service for auth state caching
builder.Services.AddBlazoredLocalStorage();

// Register authentication services
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());

// Register access token provider for API authentication
builder.Services.AddScoped<IAccessTokenProvider, ClientAccessTokenProvider>();

// Register API client with automatic token injection
builder.Services.AddRSSVibeApiClient("https+http://apiservice");

await builder.Build().RunAsync();
