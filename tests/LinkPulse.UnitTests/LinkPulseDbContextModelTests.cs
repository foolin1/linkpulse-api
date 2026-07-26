using LinkPulse.Api.Data;
using LinkPulse.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinkPulse.UnitTests;

public sealed class LinkPulseDbContextModelTests
{
    [Fact]
    public void ShortLink_ShouldUseExpectedTableName()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(
            typeof(ShortLink));

        Assert.NotNull(entityType);
        Assert.Equal("short_links", entityType.GetTableName());
    }

    [Fact]
    public void ShortCodeIndex_ShouldBeUnique()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(
            typeof(ShortLink));

        Assert.NotNull(entityType);

        var shortCodeIndex = Assert.Single(entityType.GetIndexes()
, index =>
                        index.Properties.Count == 1
                        && index.Properties[0].Name
                        == nameof(ShortLink.ShortCode));

        Assert.True(shortCodeIndex.IsUnique);
    }

    [Fact]
    public void NormalizedEmailIndex_ShouldBeUnique()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(
            typeof(ApplicationUser));

        Assert.NotNull(entityType);

        var normalizedEmailIndex = Assert.Single(entityType.GetIndexes()
, index =>
                        index.Properties.Count == 1
                        && index.Properties[0].Name
                        == nameof(ApplicationUser.NormalizedEmail));

        Assert.True(normalizedEmailIndex.IsUnique);
    }

    [Fact]
    public void ClickEvent_ShouldReferenceShortLink()
    {
        using var context = CreateDbContext();

        var entityType = context.Model.FindEntityType(
            typeof(ClickEvent));

        Assert.NotNull(entityType);

        var foreignKey = Assert.Single(entityType.GetForeignKeys()
, key =>
                        key.PrincipalEntityType.ClrType
                        == typeof(ShortLink));

        Assert.Equal(
            nameof(ClickEvent.ShortLinkId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            DeleteBehavior.Cascade,
            foreignKey.DeleteBehavior);
    }

    private static LinkPulseDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<LinkPulseDbContext>()
                .UseNpgsql(
                    "Host=localhost;"
                    + "Port=5433;"
                    + "Database=linkpulse_model_tests;"
                    + "Username=test;"
                    + "Password=test")
                .Options;

        return new LinkPulseDbContext(options);
    }
}