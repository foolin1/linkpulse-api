using LinkPulse.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkPulse.Api.Data.Configurations;

public sealed class ClickEventConfiguration
    : IEntityTypeConfiguration<ClickEvent>
{
    public void Configure(
        EntityTypeBuilder<ClickEvent> builder)
    {
        builder.ToTable("click_events");

        builder.HasKey(clickEvent => clickEvent.Id);

        builder.Property(clickEvent => clickEvent.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(clickEvent => clickEvent.ShortLinkId)
            .HasColumnName("short_link_id")
            .IsRequired();

        builder.Property(clickEvent => clickEvent.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(clickEvent => clickEvent.Referrer)
            .HasColumnName("referrer")
            .HasMaxLength(EntityConstraints.ReferrerMaxLength);

        builder.Property(clickEvent => clickEvent.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(EntityConstraints.UserAgentMaxLength);

        builder.Property(clickEvent => clickEvent.ClientIpHash)
            .HasColumnName("client_ip_hash")
            .HasMaxLength(EntityConstraints.ClientIpHashMaxLength);

        builder.HasIndex(
                clickEvent => new
                {
                    clickEvent.ShortLinkId,
                    clickEvent.OccurredAt
                })
            .HasDatabaseName(
                "ix_click_events_short_link_id_occurred_at");

        builder.HasOne(clickEvent => clickEvent.ShortLink)
            .WithMany(shortLink => shortLink.ClickEvents)
            .HasForeignKey(clickEvent => clickEvent.ShortLinkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}