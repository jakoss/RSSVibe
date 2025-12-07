using Microsoft.EntityFrameworkCore;
using RSSVibe.Data;
using System.Diagnostics;
using TickerQ.EntityFrameworkCore.DbContextFactory;

namespace RSSVibe.MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ReSharper disable once ExplicitCallerInfoArgument
        using var activity = _activitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<RssVibeDbContext>();
            await dbContext.Database.MigrateAsync(stoppingToken);

            await using var tickerQDbContext = scope.ServiceProvider.GetRequiredService<TickerQDbContext>();
            await tickerQDbContext.Database.MigrateAsync(stoppingToken);

        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }
}
