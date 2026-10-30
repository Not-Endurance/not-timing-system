using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Tests.Unit.Domain;

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
}
