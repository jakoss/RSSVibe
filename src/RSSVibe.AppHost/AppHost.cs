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
    .WithUrl("/scalar/v1", "Scalar") // TODO: add "normal" endpoint url as well
    .WithReference(rssvibeDb)
    .WaitFor(rssvibeDb)
    .WithReference(migrationService)
    .WaitForCompletion(migrationService);

builder.AddProject<Projects.RSSVibe_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
