using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RSSVibe.Data.Entities;

namespace RSSVibe.Data.Configurations;

internal sealed class FeedParseRunConfiguration : IEntityTypeConfiguration<FeedParseRun>
{
    public void Configure(EntityTypeBuilder<FeedParseRun> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.OwnsOne(x => x.ResponseHeaders, headersBuilder =>
        {
            headersBuilder.ToJson();
        });

        builder.Property(x => x.FetchedItemsCount).HasDefaultValue(0);
        builder.Property(x => x.NewItemsCount).HasDefaultValue(0);
        builder.Property(x => x.UpdatedItemsCount).HasDefaultValue(0);
        builder.Property(x => x.SkippedItemsCount).HasDefaultValue(0);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => new { x.FeedId, x.StartedAt }).IsDescending(false, true);
        builder.HasIndex(x => x.Status).HasFilter("status = 'failed'");

        // Relationships
        builder.HasOne(x => x.Feed)
            .WithMany(f => f.ParseRuns)
            .HasForeignKey(x => x.FeedId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ParseRunItems)
            .WithOne(pri => pri.FeedParseRun)
            .HasForeignKey(pri => pri.FeedParseRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
