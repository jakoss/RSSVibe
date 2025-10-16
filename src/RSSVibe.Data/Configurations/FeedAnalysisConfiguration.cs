using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RSSVibe.Data.Entities;

namespace RSSVibe.Data.Configurations;

internal sealed class FeedAnalysisConfiguration : IEntityTypeConfiguration<FeedAnalysis>
{
    public void Configure(EntityTypeBuilder<FeedAnalysis> builder)
    {
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.AnalysisStatus).HasDefaultValue(FeedAnalysisStatus.Pending);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.OwnsOne(x => x.PreflightDetails, detailsBuilder =>
        {
            detailsBuilder.ToJson();
        });

        builder.OwnsOne(x => x.Selectors, selectorsBuilder =>
        {
            selectorsBuilder.ToJson();
        });

        // Indexes
        builder.HasIndex(x => new { x.UserId, x.NormalizedUrl }).IsUnique();
        builder.HasIndex(x => new { x.AnalysisStatus, x.CreatedAt }).IsDescending(false, true);

        // Relationships
        builder.HasOne(x => x.ApprovedFeed)
            .WithOne(f => f.Analysis)
            .HasForeignKey<FeedAnalysis>(x => x.ApprovedFeedId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
