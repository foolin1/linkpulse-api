namespace LinkPulse.Api.Features.Redirects;

public interface IClickEventRecorder
{
    Task RecordAsync(
        Guid shortLinkId,
        HttpContext httpContext,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
}