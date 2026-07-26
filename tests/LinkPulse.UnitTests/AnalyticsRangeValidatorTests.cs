using LinkPulse.Api.Features.Analytics;

namespace LinkPulse.UnitTests;

public sealed class AnalyticsRangeValidatorTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(
            2026,
            7,
            27,
            12,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Validate_WithoutDates_ShouldUseDefaultRange()
    {
        var errors =
            AnalyticsRangeValidator.Validate(
                null,
                null,
                CurrentTime,
                out var range);

        Assert.Empty(errors);

        Assert.Equal(
            CurrentTime,
            range.To);

        Assert.Equal(
            CurrentTime.AddDays(
                -AnalyticsRangeValidator
                    .DefaultRangeDays),
            range.From);
    }

    [Fact]
    public void Validate_WithValidRange_ShouldAcceptRange()
    {
        var from =
            CurrentTime.AddDays(-7);

        var errors =
            AnalyticsRangeValidator.Validate(
                from,
                CurrentTime,
                CurrentTime,
                out var range);

        Assert.Empty(errors);
        Assert.Equal(from, range.From);
        Assert.Equal(CurrentTime, range.To);
    }

    [Fact]
    public void Validate_WithReversedRange_ShouldRejectRange()
    {
        var errors =
            AnalyticsRangeValidator.Validate(
                CurrentTime,
                CurrentTime.AddDays(-1),
                CurrentTime,
                out _);

        Assert.Contains(
            "from",
            errors.Keys);
    }

    [Fact]
    public void Validate_WithRangeOverMaximum_ShouldRejectRange()
    {
        var errors =
            AnalyticsRangeValidator.Validate(
                CurrentTime.AddDays(-91),
                CurrentTime,
                CurrentTime,
                out _);

        Assert.Contains(
            "range",
            errors.Keys);
    }

    [Fact]
    public void Validate_WithFutureEnd_ShouldRejectRange()
    {
        var errors =
            AnalyticsRangeValidator.Validate(
                CurrentTime.AddDays(-1),
                CurrentTime.AddHours(1),
                CurrentTime,
                out _);

        Assert.Contains(
            "to",
            errors.Keys);
    }
}