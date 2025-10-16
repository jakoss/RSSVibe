using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RSSVibe.Data.Entities;

namespace RSSVibe.Data.Configurations;

internal sealed class FeedConfiguration : IEntityTypeConfiguration<Feed>
{
    public void Configure(EntityTypeBuilder<Feed> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Language).HasMaxLength(16);

        builder.Property(x => x.TtlMinutes).HasDefaultValue((short)60);

        builder.OwnsOne(x => x.Selectors, selectorsBuilder =>
        {
            selectorsBuilder.ToJson();
        });

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(x => new { x.UserId, x.NormalizedSourceUrl }).IsUnique();
        builder.HasIndex(x => new { x.NextParseAfter, x.LastParseStatus });

        // Relationships
        builder.HasOne(x => x.Analysis)
            .WithOne(a => a.ApprovedFeed)
            .HasForeignKey<Feed>(x => x.AnalysisId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.ParseRuns)
            .WithOne(pr => pr.Feed)
            .HasForeignKey(pr => pr.FeedId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Items)
            .WithOne(i => i.Feed)
            .HasForeignKey(i => i.FeedId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
