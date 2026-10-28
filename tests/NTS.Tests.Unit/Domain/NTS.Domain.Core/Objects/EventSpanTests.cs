using System.Globalization;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Tests.Unit.Domain.NTS.Domain.Core.Objects;

/// <summary>
/// The days an Event runs, and whether it is Live (ADR-0007, #628): an Event is Live until the end of its last day, which
/// is 23:59:59 in the Event's own offset, and Historic from that second on, with no grace. Liveness is the rule of the
/// span and a clock it is given, never a field.
/// </summary>
public sealed class EventSpanTests
{
    static readonly TimeSpan SOFIA = TimeSpan.FromHours(3);

    [Fact]
    public void The_span_runs_from_the_start_of_its_first_day_to_the_last_second_of_its_last_day_in_the_offset_it_is_given()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 5, 1, 14, 30, 0, SOFIA),
            new DateTimeOffset(2026, 5, 3, 8, 15, 0, SOFIA)
        );

        Assert.Equal(new DateTimeOffset(2026, 5, 1, 0, 0, 0, SOFIA), span.StartDay);
        Assert.Equal(new DateTimeOffset(2026, 5, 3, 23, 59, 59, SOFIA), span.EndDay);
    }

    [Fact]
    public void An_Event_is_Live_until_the_second_before_the_end_of_its_last_day()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero)
        );

        Assert.True(span.IsLive(span.StartDay.AddDays(-30)));
        Assert.True(span.IsLive(span.StartDay));
        Assert.True(span.IsLive(span.EndDay.AddSeconds(-1)));
    }

    [Fact]
    public void An_Event_is_Historic_from_the_end_of_its_last_day_with_no_grace()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero)
        );

        Assert.False(span.IsLive(span.EndDay));
        Assert.False(span.IsLive(span.EndDay.AddSeconds(1)));
        Assert.False(span.IsLive(span.EndDay.AddDays(40)));
    }

    [Fact]
    public void The_end_is_the_instant_of_the_Events_offset_so_a_clock_in_another_offset_decides_the_same()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 5, 1, 9, 0, 0, SOFIA),
            new DateTimeOffset(2026, 5, 2, 9, 0, 0, SOFIA)
        );
        var endInUtc = span.EndDay.ToUniversalTime();

        Assert.Equal(new DateTimeOffset(2026, 5, 2, 20, 59, 59, TimeSpan.Zero), endInUtc);
        Assert.True(span.IsLive(endInUtc.AddSeconds(-1)));
        Assert.False(span.IsLive(endInUtc));
        Assert.True(span.IsLive(new DateTimeOffset(2026, 5, 2, 15, 59, 58, TimeSpan.FromHours(-5))));
        Assert.False(span.IsLive(new DateTimeOffset(2026, 5, 2, 15, 59, 59, TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void An_Event_asks_its_span_and_has_no_flag_of_its_own()
    {
        var span = new EventSpan(
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, SOFIA),
            new DateTimeOffset(2026, 5, 2, 0, 0, 0, SOFIA)
        );
        var country = new Country(Guid.NewGuid(), "Bulgaria", "BG", null, null);
        var @event = new EventInformation(country, "Spring Ride", "Sofia", span, null, Guid.NewGuid());

        Assert.True(@event.IsLive(span.EndDay.AddSeconds(-1)));
        Assert.False(@event.IsLive(span.EndDay));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(-5)]
    [InlineData(-11)]
    [InlineData(11)]
    public void The_days_of_an_Event_are_the_days_at_its_place_whatever_the_offset(int hoursFromUtc)
    {
        var offset = TimeSpan.FromHours(hoursFromUtc);
        var span = new EventSpan(
            new DateTimeOffset(2026, 5, 1, 14, 30, 0, offset),
            new DateTimeOffset(2026, 5, 3, 8, 15, 0, offset)
        );

        Assert.Equal(new DateOnly(2026, 5, 1), span.FirstDay);
        Assert.Equal(new DateOnly(2026, 5, 3), span.LastDay);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(-5)]
    [InlineData(-11)]
    [InlineData(11)]
    public void An_Event_that_was_stored_and_is_read_back_keeps_its_instants_and_its_days(int hoursFromUtc)
    {
        var offset = TimeSpan.FromHours(hoursFromUtc);
        var made = new EventSpan(
            new DateTimeOffset(2026, 5, 1, 9, 0, 0, offset),
            new DateTimeOffset(2026, 5, 3, 9, 0, 0, offset)
        );

        // What is stored is the instants, which the database and the Api tell in UTC.
        var read = EventSpan.Stored(made.StartDay.ToUniversalTime(), made.EndDay.ToUniversalTime());

        Assert.Equal(made.StartDay, read.StartDay);
        Assert.Equal(made.EndDay, read.EndDay);
        Assert.Equal(new DateOnly(2026, 5, 1), read.FirstDay);
        Assert.Equal(new DateOnly(2026, 5, 3), read.LastDay);
        Assert.Equal(made.IsLive(made.EndDay.AddSeconds(-1)), read.IsLive(made.EndDay.AddSeconds(-1)));
        Assert.False(read.IsLive(made.EndDay)); // and it ends when the place's last day ends
    }

    [Fact]
    public void The_first_day_is_told_for_a_place_at_UTC_plus_12_and_the_last_day_for_a_place_at_UTC_minus_12()
    {
        var ahead = TimeSpan.FromHours(12);
        var behind = TimeSpan.FromHours(-12);

        Assert.Equal(
            new DateOnly(2026, 5, 1),
            new EventSpan(
                new DateTimeOffset(2026, 5, 1, 9, 0, 0, ahead),
                new DateTimeOffset(2026, 5, 3, 9, 0, 0, ahead)
            ).FirstDay
        );
        Assert.Equal(
            new DateOnly(2026, 5, 3),
            new EventSpan(
                new DateTimeOffset(2026, 5, 1, 9, 0, 0, behind),
                new DateTimeOffset(2026, 5, 3, 9, 0, 0, behind)
            ).LastDay
        );
    }

    [Fact]
    public void Making_the_span_whole_again_in_UTC_moves_the_days_of_a_place_ahead_of_UTC_which_is_why_a_stored_span_is_kept_as_it_is()
    {
        // 2026-05-01 00:00 to 2026-05-03 23:59:59 in Sofia (UTC+3), as the database holds them.
        var first = new DateTimeOffset(2026, 4, 30, 21, 0, 0, TimeSpan.Zero);
        var last = new DateTimeOffset(2026, 5, 3, 20, 59, 59, TimeSpan.Zero);

        var kept = EventSpan.Stored(first, last);
        var remade = new EventSpan(first, last);

        Assert.Equal((first, last), (kept.StartDay, kept.EndDay));
        Assert.Equal((new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3)), (kept.FirstDay, kept.LastDay));
        Assert.Equal(new DateOnly(2026, 4, 30), remade.FirstDay); // the day before, and the end three hours late
        Assert.Equal(new DateTimeOffset(2026, 5, 3, 23, 59, 59, TimeSpan.Zero), remade.EndDay);
    }

    [Theory]
    [InlineData(-864000, 1)] // ten days before: its first day
    [InlineData(-1, 1)] // a second before it starts
    [InlineData(0, 1)] // the first second: the first day
    [InlineData(86399, 1)] // still the first day at its place
    [InlineData(86400, 2)] // and the second day from the next midnight there
    [InlineData(172799, 2)]
    [InlineData(172800, 3)]
    [InlineData(259198, 3)] // the last day while it runs
    [InlineData(259199, 3)] // from the second it ends: its last day
    [InlineData(5000000, 3)] // and after it
    public void The_day_shown_for_an_Event_is_its_first_day_before_it_the_day_at_its_place_while_it_runs_and_its_last_day_after_it(
        int secondsSinceTheFirstSecond,
        int dayShown
    )
    {
        var offset = TimeSpan.FromHours(3);
        var span = new EventSpan(
            new DateTimeOffset(2026, 5, 1, 12, 0, 0, offset),
            new DateTimeOffset(2026, 5, 3, 12, 0, 0, offset)
        );

        var text = span.Display(span.StartDay.AddSeconds(secondsSinceTheFirstSecond));

        Assert.Equal(new DateOnly(2026, 5, dayShown), DateOnly.Parse(text, CultureInfo.CurrentCulture));
    }

    [Fact]
    public void A_day_of_twenty_five_hours_does_not_show_a_day_after_the_last()
    {
        // The clocks went back on the last day of the place: the span is an hour longer than three days.
        var first = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
        var span = EventSpan.Stored(first, first.AddHours(73).AddSeconds(-1));

        var text = span.Display(first.AddHours(72).AddMinutes(30));

        Assert.Equal(new DateOnly(2026, 10, 27), span.LastDay);
        Assert.Equal(span.LastDay, DateOnly.Parse(text, CultureInfo.CurrentCulture));
    }
}
