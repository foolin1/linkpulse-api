using LinkPulse.Api.Data;
using LinkPulse.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LinkPulse.Api.Features.Redirects;

public sealed class ClickEventRecorder(
    LinkPulseDbContext dbContext,
    ILogger<ClickEventRecorder> logger)
    : IClickEventRecorder
{
    public async Task RecordAsync(
        Guid shortLinkId,
        HttpContext httpContext,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var referrer = NormalizeHeader(
            httpContext.Request.Headers.Referer.ToString(),
            EntityConstraints.ReferrerMaxLength);

        var userAgent = NormalizeHeader(
            httpContext.Request.Headers.UserAgent.ToString(),
            EntityConstraints.UserAgentMaxLength);

        var clickEvent = new ClickEvent(
            shortLinkId,
            occurredAt,
            referrer,
            userAgent,
            clientIpHash: null);

        dbContext.ClickEvents.Add(clickEvent);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateException exception)
        {
            Detach(clickEvent);

            logger.LogWarning(
                exception,
                "Click event for short link {ShortLinkId} could not be saved.",
                shortLinkId);
        }
        catch (NpgsqlException exception)
        {
            Detach(clickEvent);

            logger.LogWarning(
                exception,
                "PostgreSQL is unavailable while saving a click event for short link {ShortLinkId}.",
                shortLinkId);
        }
        catch (TimeoutException exception)
        {
            Detach(clickEvent);

            logger.LogWarning(
                exception,
                "PostgreSQL timed out while saving a click event for short link {ShortLinkId}.",
                shortLinkId);
        }
    }

    private void Detach(ClickEvent clickEvent)
    {
        dbContext.Entry(clickEvent).State =
            EntityState.Detached;
    }

    private static string? NormalizeHeader(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();

        return normalizedValue.Length <= maximumLength
            ? normalizedValue
            : normalizedValue[..maximumLength];
    }
}