using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RSSVibe.Data.Abstractions;

namespace RSSVibe.Data.Interceptors;

internal sealed class UpdateTimestampsInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            UpdateTimestamps(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            UpdateTimestamps(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private static void UpdateTimestamps(DbContext context)
    {
        var now = DateTimeOffset.UtcNow;

        var entries = context.ChangeTracker.Entries<IAuditableEntity>()
            .Where(e => e.State is EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = now;
        }
    }
}
