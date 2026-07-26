using LinkPulse.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkPulse.Api.Data;

public sealed class LinkPulseDbContext(
    DbContextOptions<LinkPulseDbContext> options)
    : DbContext(options)
{
    public DbSet<ApplicationUser> ApplicationUsers =>
        Set<ApplicationUser>();

    public DbSet<ShortLink> ShortLinks =>
        Set<ShortLink>();

    public DbSet<ClickEvent> ClickEvents =>
        Set<ClickEvent>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(LinkPulseDbContext).Assembly);
    }
}