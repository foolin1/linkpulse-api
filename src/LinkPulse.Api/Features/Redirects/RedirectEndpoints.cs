using LinkPulse.Api.Caching;
using LinkPulse.Api.Data;
using LinkPulse.Api.Features.Links;
using Microsoft.EntityFrameworkCore;

namespace LinkPulse.Api.Features.Redirects;

public static class RedirectEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet(
                "/{shortCode}",
                RedirectAsync)
            .AllowAnonymous()
            .WithName("RedirectShortLink")
            .WithTags("Redirects");
    }

    private static async Task<IResult> RedirectAsync(
        string shortCode,
        HttpContext httpContext,
        LinkPulseDbContext dbContext,
        ILinkCache linkCache,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!IsValidShortCode(shortCode))
        {
            return LinkNotFound();
        }

        var normalizedShortCode =
            ShortLinkInputValidator.NormalizeAlias(
                shortCode);

        var currentTime =
            timeProvider.GetUtcNow();

        var cacheResult =
            await linkCache.GetAsync(
                normalizedShortCode,
                cancellationToken);

        if (cacheResult.Status
                == LinkCacheLookupStatus.Hit
            && cacheResult.CachedLink is not null)
        {
            var cachedLink =
                cacheResult.CachedLink;

            if (!cachedLink.IsExpired(currentTime)
                && IsAllowedTargetUrl(
                    cachedLink.TargetUrl))
            {
                httpContext.Response.Headers[
                    "X-LinkPulse-Cache"] = "HIT";

                return Results.Redirect(
                    cachedLink.TargetUrl,
                    permanent: false,
                    preserveMethod: false);
            }

            await linkCache.RemoveAsync(
                normalizedShortCode,
                cancellationToken);

            httpContext.Response.Headers[
                "X-LinkPulse-Cache"] = "STALE";
        }
        else if (cacheResult.Status
                 == LinkCacheLookupStatus.Unavailable)
        {
            httpContext.Response.Headers[
                "X-LinkPulse-Cache"] = "BYPASS";
        }
        else
        {
            httpContext.Response.Headers[
                "X-LinkPulse-Cache"] = "MISS";
        }

        var shortLink = await dbContext.ShortLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                link =>
                    link.ShortCode
                    == normalizedShortCode,
                cancellationToken);

        if (shortLink is null)
        {
            return LinkNotFound();
        }

        var isExpired =
            shortLink.ExpiresAt.HasValue
            && shortLink.ExpiresAt.Value
            <= currentTime;

        if (!shortLink.IsActive || isExpired)
        {
            await linkCache.RemoveAsync(
                normalizedShortCode,
                cancellationToken);

            return LinkUnavailable();
        }

        if (!IsAllowedTargetUrl(
                shortLink.TargetUrl))
        {
            return Results.Problem(
                statusCode:
                    StatusCodes.Status500InternalServerError,
                title: "Invalid redirect target",
                detail:
                    "The stored target URL cannot be used for a redirect.");
        }

        var cachedLinkFromDatabase =
            new CachedLink(
                shortLink.Id,
                shortLink.ShortCode,
                shortLink.TargetUrl,
                shortLink.ExpiresAt);

        await linkCache.SetAsync(
            cachedLinkFromDatabase,
            currentTime,
            cancellationToken);

        return Results.Redirect(
            shortLink.TargetUrl,
            permanent: false,
            preserveMethod: false);
    }

    private static bool IsValidShortCode(
        string shortCode)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            return false;
        }

        var normalizedShortCode =
            shortCode.Trim();

        if (normalizedShortCode.Length < 1
            || normalizedShortCode.Length
            > EntityConstraints.ShortCodeMaxLength)
        {
            return false;
        }

        return normalizedShortCode.All(
            character =>
                char.IsAsciiLetterOrDigit(character)
                || character == '-'
                || character == '_');
    }

    private static bool IsAllowedTargetUrl(
        string targetUrl)
    {
        return Uri.TryCreate(
                   targetUrl,
                   UriKind.Absolute,
                   out var uri)
               && !string.IsNullOrWhiteSpace(uri.Host)
               && (uri.Scheme == Uri.UriSchemeHttp
                   || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static IResult LinkNotFound()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Short link was not found",
            detail:
                "No short link exists for the specified code.");
    }

    private static IResult LinkUnavailable()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status410Gone,
            title: "Short link is unavailable",
            detail:
                "The short link is disabled or has expired.");
    }
}