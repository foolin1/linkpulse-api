using LinkPulse.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkPulse.Api.Data.Configurations;

public sealed class ShortLinkConfiguration
    : IEntityTypeConfiguration<ShortLink>
{
    public void Configure(
        EntityTypeBuilder<ShortLink> builder)
    {
        builder.ToTable(
            "short_links",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_short_links_short_code_not_empty",
                    "char_length(short_code) > 0");

                tableBuilder.HasCheckConstraint(
                    "ck_short_links_expiration",
                    "expires_at IS NULL OR expires_at > created_at");
            });

        builder.HasKey(shortLink => shortLink.Id);

        builder.Property(shortLink => shortLink.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(shortLink => shortLink.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(shortLink => shortLink.ShortCode)
            .HasColumnName("short_code")
            .HasMaxLength(EntityConstraints.ShortCodeMaxLength)
            .IsRequired();

        builder.Property(shortLink => shortLink.TargetUrl)
            .HasColumnName("target_url")
            .HasMaxLength(EntityConstraints.TargetUrlMaxLength)
            .IsRequired();

        builder.Property(shortLink => shortLink.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(shortLink => shortLink.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(shortLink => shortLink.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(shortLink => shortLink.ShortCode)
            .IsUnique()
            .HasDatabaseName("ux_short_links_short_code");

        builder.HasIndex(
                shortLink => new
                {
                    shortLink.OwnerId,
                    shortLink.CreatedAt
                })
            .HasDatabaseName("ix_short_links_owner_id_created_at");

        builder.HasIndex(
                shortLink => new
                {
                    shortLink.IsActive,
                    shortLink.ExpiresAt
                })
            .HasDatabaseName("ix_short_links_is_active_expires_at");
    }
}