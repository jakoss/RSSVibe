var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgres")
    .WithContainerName("rssvibe-postgres")
    // .WithDataVolume()
    // We need to have custom volume for until aspire supports postgres 18: https://github.com/dotnet/aspire/issues/11710
    .WithVolume("rssvibe-postgres-data", "/var/lib/postgresql/18/docker")
    .WithImageTag("18")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDbGate(configureContainer: resourceBuilder =>
    {
        resourceBuilder.WithLifetime(ContainerLifetime.Persistent);
    }, "rssvibe-dbgate");

var rssvibeDb = postgresServer.AddDatabase("rssvibedb");

var migrationService = builder.AddProject<Projects.RSSVibe_MigrationService>("migrationservice")
    .WithReference(rssvibeDb)
    .WaitFor(rssvibeDb);

var apiService = builder.AddProject<Projects.RSSVibe_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithUrls(context =>
    {
        if (context.Urls.Any(e => e.Endpoint?.EndpointName.StartsWith("https", StringComparison.OrdinalIgnoreCase) != true))
        {
            return;
        }
        foreach (var url in context.Urls)
        {
            switch (url.Endpoint?.EndpointName)
            {
                case "https":
                    url.DisplayText = "Scalar";
                    url.Url = "/scalar";
                    break;
                case "https2":
                    url.DisplayText = "TickerQ";
                    url.Url = "/admin/jobs";
                    break;
            }
        }
    })
    .WithReference(rssvibeDb)
    .WaitFor(rssvibeDb)
    .WithReference(migrationService)
    .WaitForCompletion(migrationService);

// Standalone Blazor WebAssembly frontend
var frontend = builder.AddStandaloneBlazorWebAssemblyProject<Projects.RSSVibe_Client>("frontend")
    .WithReference(apiService);

// Enable CORS for the frontend
apiService.WithReference(frontend);

builder.Build().Run();
