namespace LinkPulse.Api.Features.Analytics;

public sealed record AnalyticsRange(
    DateTimeOffset From,
    DateTimeOffset To);

public sealed record LinkAnalyticsResponse(
    Guid LinkId,
    string ShortCode,
    DateTimeOffset From,
    DateTimeOffset To,
    long TotalClicks,
    IReadOnlyList<AnalyticsPointResponse> TimeSeries,
    IReadOnlyList<ReferrerAnalyticsResponse> TopReferrers);

public sealed record AnalyticsPointResponse(
    string Date,
    long Clicks);

public sealed record ReferrerAnalyticsResponse(
    string Referrer,
    long Clicks);

public sealed record ClickEventResponse(
    long Id,
    DateTimeOffset OccurredAt,
    string? Referrer,
    string? UserAgent);

public sealed record PagedClickEventsResponse(
    int Page,
    int PageSize,
    long TotalCount,
    long TotalPages,
    IReadOnlyList<ClickEventResponse> Items);