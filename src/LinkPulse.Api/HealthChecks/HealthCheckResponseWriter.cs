using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LinkPulse.Api.HealthChecks;

public static class HealthCheckResponseWriter
{
    public static Task WriteAsync(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(
                report.TotalDuration.TotalMilliseconds,
                2),
            checks = report.Entries.Select(
                entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    durationMs = Math.Round(
                        entry.Value.Duration.TotalMilliseconds,
                        2)
                })
        };

        return context.Response.WriteAsJsonAsync(response);
    }
}