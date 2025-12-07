using Microsoft.EntityFrameworkCore;
using RSSVibe.Data.Extensions;
using RSSVibe.MigrationService;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.EntityFrameworkCore.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHostedService<Worker>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddRssVibeDatabase("rssvibedb");

builder.Services.AddTickerQ(options =>
{
    options.AddOperationalStore(storeBuilder =>
    {
        storeBuilder.UseTickerQDbContext<TickerQDbContext>(optBuilder =>
        {
            var connectionString = builder.Configuration.GetConnectionString("rssvibedb")!;
            optBuilder.UseNpgsql(connectionString, optionsBuilder =>
            {
                optionsBuilder.MigrationsAssembly("RSSVibe.Data");
                optionsBuilder.MigrationsHistoryTable("__EFMigrationsHistory", "tickerq");
            });
        }, schema: "tickerq");
    });
    options.DisableBackgroundServices();
});
builder.EnrichNpgsqlDbContext<TickerQDbContext>();

var host = builder.Build();
host.Run();
