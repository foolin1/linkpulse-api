namespace LinkPulse.Api.Features.Links;

public sealed record CreateShortLinkRequest(
    string? TargetUrl,
    string? CustomAlias,
    DateTimeOffset? ExpiresAt);

public sealed record UpdateShortLinkRequest(
    string? TargetUrl,
    DateTimeOffset? ExpiresAt);

public sealed record ShortLinkResponse(
    Guid Id,
    string ShortCode,
    string ShortUrl,
    string TargetUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool IsActive,
    bool IsExpired);

public sealed record PagedShortLinksResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<ShortLinkResponse> Items);