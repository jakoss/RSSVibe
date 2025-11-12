var builder = DistributedApplication.CreateBuilder(args);

var postgresServer = builder.AddPostgres("postgres")
    .WithDataVolume("rssvibe-postgres-data")
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
    .WithUrl("/scalar/v1", "Scalar") // TODO: Add endpoint for the api itself
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
