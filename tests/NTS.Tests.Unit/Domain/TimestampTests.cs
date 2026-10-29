using NTS.Domain.Objects;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// How times are compared (#612, ADR-0004): by the time of the day, so that a ride that crosses midnight stays
/// unsupported, and "at or after" includes the instant itself, which is what places a Snapshot stamped at the Start of the
/// next Phase in that Phase.
/// </summary>
public sealed class TimestampTests
{
    static readonly DateTimeOffset NINE = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void At_or_after_includes_the_same_instant_and_after_does_not()
    {
        var first = new Timestamp(NINE);
        var same = new Timestamp(NINE);

        Assert.True(first >= same);
        Assert.True(first <= same);
        Assert.False(first > same);
        Assert.False(first < same);
    }

    [Fact]
    public void At_or_after_is_true_for_a_later_time_and_false_for_an_earlier_one()
    {
        var earlier = new Timestamp(NINE);
        var later = new Timestamp(NINE.AddSeconds(1));

        Assert.True(later >= earlier);
        Assert.False(earlier >= later);
        Assert.True(earlier <= later);
        Assert.False(later <= earlier);
    }

    [Fact]
    public void Times_are_compared_by_the_time_of_the_day_and_not_by_the_date()
    {
        var yesterdayLater = new Timestamp(NINE.AddDays(-1).AddHours(2));
        var today = new Timestamp(NINE);

        Assert.True(yesterdayLater > today);
        Assert.True(yesterdayLater >= today);
    }

    [Fact]
    public void A_time_that_is_missing_is_neither_before_nor_at_or_after_any_time()
    {
        var time = new Timestamp(NINE);

        Assert.False(time >= null);
        Assert.False(null >= time);
        Assert.False(time > null);
        Assert.False(time < null);
    }
}
