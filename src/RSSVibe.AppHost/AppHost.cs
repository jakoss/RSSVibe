var builder = DistributedApplication.CreateBuilder(args);

var postgresUsername = builder.AddParameter("postgres-username", "postgres");
var postgresPassword = builder.AddParameter("postgres-password", "P@ssw0rd!");

var postgresServer = builder.AddPostgres("postgres", postgresUsername, postgresPassword)
    .WithDataVolume("rssvibe-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDbGate();

var rssvibeDb = postgresServer.AddDatabase("rssvibe-db");

var apiService = builder.AddProject<Projects.RSSVibe_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(rssvibeDb)
    .WaitFor(rssvibeDb);

builder.AddProject<Projects.RSSVibe_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
