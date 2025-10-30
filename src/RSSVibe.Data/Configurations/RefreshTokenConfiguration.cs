using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RSSVibe.Data.Entities;

namespace RSSVibe.Data.Configurations;

/// <summary>
/// Entity Framework configuration for RefreshToken entity.
/// Defines table structure, indexes, and relationships.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        // Primary key configuration
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .ValueGeneratedNever(); // Application generates UUIDv7

        // Token configuration - base64 encoded 64 bytes = ~88 chars, allow headroom
        builder.Property(rt => rt.Token)
            .IsRequired()
            .HasMaxLength(512);

        // Timestamp configurations
        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(rt => rt.RevokedAt)
            .IsRequired(false);

        // IsUsed flag for replay detection
        builder.Property(rt => rt.IsUsed)
            .IsRequired()
            .HasDefaultValue(false);

        // Index on Token for fast lookup and prevent duplicates
        builder.HasIndex(rt => rt.Token)
            .IsUnique()
            .HasDatabaseName("IX_RefreshTokens_Token");

        // Index on UserId for querying user's active tokens
        builder.HasIndex(rt => rt.UserId)
            .HasDatabaseName("IX_RefreshTokens_UserId");

        // Index on ExpiresAt for cleanup job
        builder.HasIndex(rt => rt.ExpiresAt)
            .HasDatabaseName("IX_RefreshTokens_ExpiresAt");

        // Foreign key relationship to ApplicationUser
        builder.HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
