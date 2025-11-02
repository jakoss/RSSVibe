using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RSSVibe.Data.Entities;
using RSSVibe.Data.Interceptors;

namespace RSSVibe.Data.Extensions;

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder AddRssVibeDatabase(this IHostApplicationBuilder builder, string connectionName)
    {
        builder.AddNpgsqlDbContext<RssVibeDbContext>(
            connectionName,
            configureDbContextOptions: options =>
            {
                options.AddInterceptors(new UpdateTimestampsInterceptor());
            });

        builder.Services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<RssVibeDbContext>();

        return builder;
    }
}
