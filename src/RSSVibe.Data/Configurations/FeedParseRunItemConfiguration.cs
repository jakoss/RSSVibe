using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RSSVibe.Data.Entities;

namespace RSSVibe.Data.Configurations;

internal sealed class FeedParseRunItemConfiguration : IEntityTypeConfiguration<FeedParseRunItem>
{
    public void Configure(EntityTypeBuilder<FeedParseRunItem> builder)
    {
        builder.HasKey(x => new { x.FeedParseRunId, x.FeedItemId });

        builder.Property(x => x.SeenAt).HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => new { x.FeedItemId, x.SeenAt }).IsDescending(false, true);
        builder.HasIndex(x => new { x.FeedParseRunId, x.ChangeKind });

        // Relationships
        builder.HasOne(x => x.FeedParseRun)
            .WithMany(pr => pr.ParseRunItems)
            .HasForeignKey(x => x.FeedParseRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FeedItem)
            .WithMany(i => i.ParseRunItems)
            .HasForeignKey(x => x.FeedItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
