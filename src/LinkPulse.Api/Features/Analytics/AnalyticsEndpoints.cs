using System.Globalization;
using System.Security.Claims;
using LinkPulse.Api.Authentication;
using LinkPulse.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LinkPulse.Api.Features.Analytics;

public static class AnalyticsEndpoints
{
    private const int DefaultPage = 1;

    private const int DefaultPageSize = 50;

    private const int MaximumPageSize = 100;

    private const int TopReferrersCount = 5;

    private const string DirectReferrer = "(direct)";

    public static void Map(WebApplication app)
    {
        var group = app
            .MapGroup("/api/links/{id:guid}")
            .RequireAuthorization()
            .WithTags("Analytics");

        group.MapGet(
                "/analytics",
                GetAnalyticsAsync)
            .WithName("GetLinkAnalytics");

        group.MapGet(
                "/events",
                GetClickEventsAsync)
            .WithName("GetLinkClickEvents");
    }

    private static async Task<IResult> GetAnalyticsAsync(
        Guid id,
        DateTimeOffset? from,
        DateTimeOffset? to,
        ClaimsPrincipal principal,
        LinkPulseDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var ownerId = principal.GetUserId();

        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var currentTime =
            timeProvider.GetUtcNow();

        var validationErrors =
            AnalyticsRangeValidator.Validate(
                from,
                to,
                currentTime,
                out var range);

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(
                validationErrors);
        }

        var shortLink = await dbContext.ShortLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                link =>
                    link.Id == id
                    && link.OwnerId == ownerId.Value,
                cancellationToken);

        if (shortLink is null)
        {
            return LinkNotFound();
        }

        var clickEventsQuery = dbContext.ClickEvents
            .AsNoTracking()
            .Where(
                clickEvent =>
                    clickEvent.ShortLinkId == id
                    && clickEvent.OccurredAt >= range.From
                    && clickEvent.OccurredAt < range.To);

        var occurrenceTimes =
            await clickEventsQuery
                .Select(
                    clickEvent =>
                        clickEvent.OccurredAt)
                .ToListAsync(cancellationToken);

        var groupedReferrers =
            await clickEventsQuery
                .GroupBy(
                    clickEvent =>
                        clickEvent.Referrer)
                .Select(
                    group =>
                        new
                        {
                            Referrer =
                                group.Key
                                ?? DirectReferrer,

                            Clicks =
                                group.LongCount()
                        })
                .OrderByDescending(
                    item => item.Clicks)
                .ThenBy(
                    item => item.Referrer)
                .Take(TopReferrersCount)
                .ToListAsync(cancellationToken);

        var clicksByDate = occurrenceTimes
            .GroupBy(
                occurredAt =>
                    DateOnly.FromDateTime(
                        occurredAt.UtcDateTime))
            .ToDictionary(
                group => group.Key,
                group => (long)group.Count());

        var timeSeries = BuildTimeSeries(
            range,
            clicksByDate);

        var topReferrers = groupedReferrers
            .Select(
                item =>
                    new ReferrerAnalyticsResponse(
                        item.Referrer,
                        item.Clicks))
            .ToArray();

        return Results.Ok(
            new LinkAnalyticsResponse(
                shortLink.Id,
                shortLink.ShortCode,
                range.From,
                range.To,
                occurrenceTimes.Count,
                timeSeries,
                topReferrers));
    }

    private static async Task<IResult> GetClickEventsAsync(
        Guid id,
        int? page,
        int? pageSize,
        ClaimsPrincipal principal,
        LinkPulseDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ownerId = principal.GetUserId();

        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var paginationErrors =
            ValidatePagination(
                page,
                pageSize,
                out var resolvedPage,
                out var resolvedPageSize);

        if (paginationErrors.Count > 0)
        {
            return Results.ValidationProblem(
                paginationErrors);
        }

        var linkExists = await dbContext.ShortLinks
            .AsNoTracking()
            .AnyAsync(
                link =>
                    link.Id == id
                    && link.OwnerId == ownerId.Value,
                cancellationToken);

        if (!linkExists)
        {
            return LinkNotFound();
        }

        var query = dbContext.ClickEvents
            .AsNoTracking()
            .Where(
                clickEvent =>
                    clickEvent.ShortLinkId == id);

        var totalCount =
            await query.LongCountAsync(
                cancellationToken);

        var events = await query
            .OrderByDescending(
                clickEvent =>
                    clickEvent.OccurredAt)
            .ThenByDescending(
                clickEvent =>
                    clickEvent.Id)
            .Skip(
                (resolvedPage - 1)
                * resolvedPageSize)
            .Take(resolvedPageSize)
            .Select(
                clickEvent =>
                    new ClickEventResponse(
                        clickEvent.Id,
                        clickEvent.OccurredAt,
                        clickEvent.Referrer,
                        clickEvent.UserAgent))
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (long)Math.Ceiling(
                totalCount
                / (double)resolvedPageSize);

        return Results.Ok(
            new PagedClickEventsResponse(
                resolvedPage,
                resolvedPageSize,
                totalCount,
                totalPages,
                events));
    }

    private static IReadOnlyList<AnalyticsPointResponse>
        BuildTimeSeries(
            AnalyticsRange range,
            IReadOnlyDictionary<DateOnly, long>
                clicksByDate)
    {
        var firstDate =
            DateOnly.FromDateTime(
                range.From.UtcDateTime);

        var lastIncludedInstant =
            range.To.AddTicks(-1);

        var lastDate =
            DateOnly.FromDateTime(
                lastIncludedInstant.UtcDateTime);

        var result =
            new List<AnalyticsPointResponse>();

        for (var date = firstDate;
             date <= lastDate;
             date = date.AddDays(1))
        {
            var clicks = clicksByDate.TryGetValue(
                date,
                out var count)
                ? count
                : 0;

            result.Add(
                new AnalyticsPointResponse(
                    date.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture),
                    clicks));
        }

        return result;
    }

    private static Dictionary<string, string[]>
        ValidatePagination(
            int? page,
            int? pageSize,
            out int resolvedPage,
            out int resolvedPageSize)
    {
        resolvedPage =
            page ?? DefaultPage;

        resolvedPageSize =
            pageSize ?? DefaultPageSize;

        var errors = new Dictionary<string, string[]>(
            StringComparer.Ordinal);

        if (resolvedPage < 1)
        {
            errors["page"] =
            [
                "Page must be a positive integer."
            ];
        }

        if (resolvedPageSize < 1
            || resolvedPageSize > MaximumPageSize)
        {
            errors["pageSize"] =
            [
                $"Page size must be between 1 and {MaximumPageSize}."
            ];
        }

        return errors;
    }

    private static IResult LinkNotFound()
    {
        return Results.Problem(
            statusCode:
                StatusCodes.Status404NotFound,
            title: "Short link was not found",
            detail:
                "The link does not exist or does not belong to the current user.");
    }
}