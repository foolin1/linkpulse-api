using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LinkPulse.Api.Caching;

public sealed class RedisLinkCache(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<LinkCacheOptions> options,
    ILogger<RedisLinkCache> logger) : ILinkCache
{
    private readonly LinkCacheOptions cacheOptions =
        options.Value;

    public async Task<LinkCacheLookupResult> GetAsync(
        string shortCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            shortCode);

        var cacheKey = BuildKey(shortCode);

        try
        {
            var database =
                connectionMultiplexer.GetDatabase();

            var value = await database
                .StringGetAsync(cacheKey)
                .WaitAsync(cancellationToken);

            if (value.IsNullOrEmpty)
            {
                return new LinkCacheLookupResult(
                    LinkCacheLookupStatus.Miss,
                    null);
            }

            CachedLink? cachedLink;

            try
            {
                cachedLink =
                    JsonSerializer.Deserialize<CachedLink>(
                        value.ToString());
            }
            catch (JsonException exception)
            {
                logger.LogWarning(
                    exception,
                    "Redis contains an invalid cached link at key {CacheKey}.",
                    cacheKey);

                await TryDeleteInvalidEntryAsync(
                    cacheKey,
                    cancellationToken);

                return new LinkCacheLookupResult(
                    LinkCacheLookupStatus.Miss,
                    null);
            }

            if (cachedLink is null)
            {
                await TryDeleteInvalidEntryAsync(
                    cacheKey,
                    cancellationToken);

                return new LinkCacheLookupResult(
                    LinkCacheLookupStatus.Miss,
                    null);
            }

            return new LinkCacheLookupResult(
                LinkCacheLookupStatus.Hit,
                cachedLink);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RedisException exception)
        {
            logger.LogWarning(
                exception,
                "Redis is unavailable while reading short code {ShortCode}.",
                shortCode);

            return new LinkCacheLookupResult(
                LinkCacheLookupStatus.Unavailable,
                null);
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(
                exception,
                "Redis timed out while reading short code {ShortCode}.",
                shortCode);

            return new LinkCacheLookupResult(
                LinkCacheLookupStatus.Unavailable,
                null);
        }
    }

    public async Task SetAsync(
        CachedLink cachedLink,
        DateTimeOffset currentTime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cachedLink);

        var defaultTtl = TimeSpan.FromMinutes(
            cacheOptions.DefaultTtlMinutes);

        var ttl = LinkCachePolicy.CalculateTtl(
            cachedLink.ExpiresAt,
            currentTime,
            defaultTtl);

        if (!ttl.HasValue)
        {
            return;
        }

        var cacheKey = BuildKey(
            cachedLink.ShortCode);

        var serializedLink =
            JsonSerializer.Serialize(cachedLink);

        try
        {
            var database =
                connectionMultiplexer.GetDatabase();

            await database
                .StringSetAsync(
                    cacheKey,
                    serializedLink,
                    ttl.Value)
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RedisException exception)
        {
            logger.LogWarning(
                exception,
                "Redis is unavailable while caching short code {ShortCode}.",
                cachedLink.ShortCode);
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(
                exception,
                "Redis timed out while caching short code {ShortCode}.",
                cachedLink.ShortCode);
        }
    }

    public async Task RemoveAsync(
        string shortCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            shortCode);

        var cacheKey = BuildKey(shortCode);

        try
        {
            var database =
                connectionMultiplexer.GetDatabase();

            await database
                .KeyDeleteAsync(cacheKey)
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RedisException exception)
        {
            logger.LogWarning(
                exception,
                "Redis is unavailable while removing short code {ShortCode}.",
                shortCode);
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(
                exception,
                "Redis timed out while removing short code {ShortCode}.",
                shortCode);
        }
    }

    private async Task TryDeleteInvalidEntryAsync(
        RedisKey cacheKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var database =
                connectionMultiplexer.GetDatabase();

            await database
                .KeyDeleteAsync(cacheKey)
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RedisException exception)
        {
            logger.LogWarning(
                exception,
                "Redis failed to remove invalid key {CacheKey}.",
                cacheKey);
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(
                exception,
                "Redis timed out while removing invalid key {CacheKey}.",
                cacheKey);
        }
    }

    private RedisKey BuildKey(string shortCode)
    {
        var normalizedShortCode =
            shortCode.Trim().ToLowerInvariant();

        return $"{cacheOptions.KeyPrefix}:{normalizedShortCode}";
    }
}