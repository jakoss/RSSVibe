using RSSVibe.Data.Extensions;
using RSSVibe.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddRssVibeDatabase("rssvibe-db");

var host = builder.Build();
host.Run();
