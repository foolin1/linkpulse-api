namespace LinkPulse.Api.Features.Analytics;

public static class AnalyticsRangeValidator
{
    public const int DefaultRangeDays = 30;

    public const int MaximumRangeDays = 90;

    private static readonly TimeSpan FutureTolerance =
        TimeSpan.FromMinutes(1);

    public static Dictionary<string, string[]> Validate(
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset currentTime,
        out AnalyticsRange range)
    {
        var resolvedTo = to ?? currentTime;

        var resolvedFrom = from
            ?? resolvedTo.AddDays(-DefaultRangeDays);

        range = new AnalyticsRange(
            resolvedFrom,
            resolvedTo);

        var errors = new Dictionary<string, string[]>(
            StringComparer.Ordinal);

        if (resolvedFrom >= resolvedTo)
        {
            errors["from"] =
            [
                "The beginning of the analytics range must be earlier than its end."
            ];
        }

        if (resolvedTo - resolvedFrom
            > TimeSpan.FromDays(MaximumRangeDays))
        {
            errors["range"] =
            [
                $"Analytics can be requested for no more than {MaximumRangeDays} days."
            ];
        }

        if (resolvedTo
            > currentTime.Add(FutureTolerance))
        {
            errors["to"] =
            [
                "The end of the analytics range cannot be in the future."
            ];
        }

        return errors;
    }
}