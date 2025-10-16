using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RSSVibe.Data.Entities;

namespace RSSVibe.Data;

public sealed class RssVibeDbContext(DbContextOptions<RssVibeDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<FeedAnalysis> FeedAnalyses => Set<FeedAnalysis>();
    public DbSet<Feed> Feeds => Set<Feed>();
    public DbSet<FeedParseRun> FeedParseRuns => Set<FeedParseRun>();
    public DbSet<FeedItem> FeedItems => Set<FeedItem>();
    public DbSet<FeedParseRunItem> FeedParseRunItems => Set<FeedParseRunItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply entity configurations
        builder.ApplyConfigurationsFromAssembly(typeof(RssVibeDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Convert all enums to strings globally
        configurationBuilder.Properties<Enum>()
            .HaveConversion<string>();
    }
}
