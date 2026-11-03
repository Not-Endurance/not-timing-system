using NoTiming.Ui.Features.Participants;

namespace NTS.Tests.Unit.NoTiming.Ui.Features.Participants;

/// <summary>The day an Event starts on is the day of its place, for a viewer in any zone (#639, ADR-0010).</summary>
public sealed class EventDayTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(-8)]
    [InlineData(12)]
    [InlineData(-11)]
    public void An_Event_that_starts_at_midnight_in_a_zone_starts_on_that_day_whatever_the_zone(int hoursFromUtc)
    {
        var firstSecond = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.FromHours(hoursFromUtc));

        Assert.Equal(new DateTime(2026, 10, 8), EventDay.Of(firstSecond));
    }

    [Fact]
    public void The_day_is_the_same_whichever_offset_the_instant_is_told_in()
    {
        var told = new DateTimeOffset(2026, 10, 7, 21, 0, 0, TimeSpan.Zero); // midnight of the 8th at UTC+3, as the Api tells it

        Assert.Equal(new DateTime(2026, 10, 8), EventDay.Of(told));
        Assert.Equal(new DateTime(2026, 10, 8), EventDay.Of(told.ToOffset(TimeSpan.FromHours(3))));
        Assert.Equal(new DateTime(2026, 10, 8), EventDay.Of(told.ToOffset(TimeSpan.FromHours(-7))));
    }

    [Fact]
    public void A_day_at_the_turn_of_a_month_and_of_a_year_is_a_day_of_its_own()
    {
        Assert.Equal(new DateTime(2027, 1, 1), EventDay.Of(new DateTimeOffset(2026, 12, 31, 22, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTime(2026, 3, 1), EventDay.Of(new DateTimeOffset(2026, 2, 28, 22, 0, 0, TimeSpan.Zero)));
    }
}
