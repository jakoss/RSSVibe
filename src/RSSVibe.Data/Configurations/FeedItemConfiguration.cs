using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RSSVibe.Data.Entities;

namespace RSSVibe.Data.Configurations;

internal sealed class FeedItemConfiguration : IEntityTypeConfiguration<FeedItem>
{
    public void Configure(EntityTypeBuilder<FeedItem> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Fingerprint).HasMaxLength(64).IsFixedLength();
        builder.Property(x => x.SourceUrl).HasMaxLength(2048);
        builder.Property(x => x.NormalizedSourceUrl).HasMaxLength(2048);
        builder.Property(x => x.Title).HasMaxLength(512);
        builder.Property(x => x.Summary).HasMaxLength(2048);

        builder.ComplexProperty(x => x.RawMetadata, metadataBuilder =>
        {
            metadataBuilder.ToJson();
        });

        builder.Property(x => x.DiscoveredAt).HasDefaultValueSql("now()");
        builder.Property(x => x.LastSeenAt).HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => new { x.FeedId, x.NormalizedSourceUrl }).IsUnique();
        builder.HasIndex(x => new { x.FeedId, x.Fingerprint }).IsUnique();
        builder.HasIndex(x => new { x.FeedId, x.PublishedAt }).IsDescending(false, true);
        builder.HasIndex(x => new { x.FeedId, x.LastSeenAt }).IsDescending(false, true);

        // Relationships
        builder.HasOne(x => x.Feed)
            .WithMany(f => f.Items)
            .HasForeignKey(x => x.FeedId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FirstParseRun)
            .WithMany()
            .HasForeignKey(x => x.FirstParseRunId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.LastParseRun)
            .WithMany()
            .HasForeignKey(x => x.LastParseRunId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.ParseRunItems)
            .WithOne(pri => pri.FeedItem)
            .HasForeignKey(pri => pri.FeedItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
